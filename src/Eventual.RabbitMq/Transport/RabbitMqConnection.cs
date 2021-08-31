namespace Eventual.Transport
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Configuration;
    using Infrastructure;
    using Infrastructure.BrokerStrategies;
    using Microsoft.Extensions.Logging;
    using Middleware;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    public class RabbitMqConnection : IConnection
    {
        private readonly ILogger<RabbitMqConnection> _logger;
        private readonly RabbitMqBusConfiguration _busConfiguration;
        private RabbitMQ.Client.IConnection _connection;
        private IModel _channel;
        readonly ConnectionFactory _factory;

        private readonly Lock _exchangeLock = new();
        private readonly Dictionary<string, bool> _exchanges = new();



        public RabbitMqConnection(RabbitMqBusConfiguration busConfiguration, ILogger<RabbitMqConnection> logger)
        {
            _logger = logger;
            _busConfiguration = busConfiguration;

            //setup connection and channels
            _factory = new ConnectionFactory
            {
                Uri = new Uri(busConfiguration.ConnectionString),
                AutomaticRecoveryEnabled = true, //wanted this to be explicit
                ClientProvidedName = busConfiguration.ServiceName
            };

            SetupGlobalExchanges();
        }

        private void SetupConnection()
        {
            if (_connection != null && _connection.IsOpen) return;

            _connection?.Dispose();

            _connection = _factory.CreateConnection();
            _connection.ConnectionShutdown += _connection_ConnectionShutdown;
            _logger.LogInformation("Connection is ready");

        }

        private void _connection_RecoverySucceeded(object sender, EventArgs e)
        {
            _logger.LogInformation("Connection Recovered");
        }

        private void _connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            _logger.LogWarning($"Lost connection:{Environment.NewLine}{e}");
        }


        private void SetupChannel()
        {
            if (_channel != null && _channel.IsOpen) return;

            _channel?.Dispose();
            SetupConnection();

            _channel = _connection.CreateModel();
            _logger.LogInformation("Channel is ready");
        }


        private void SetupGlobalExchanges()
        {
            SetupChannel();

            //setup global exchange
            _channel.ExchangeDeclare(_busConfiguration.RoutingExchangeName, "topic", true);

            //setup failed
            _channel.ExchangeDeclare(_busConfiguration.FailedExchangeName, "fanout", true);
            QueueDeclareOk failedQueue = _channel.QueueDeclare(_busConfiguration.FailedQueueName, true, false);
            _channel.QueueBind(failedQueue.QueueName, _busConfiguration.FailedExchangeName, "");

            var failedConsumer = new EventingBasicConsumer(_channel);

            void FailedEvent(object sender, BasicDeliverEventArgs args)
            {
                var countValue = args.BasicProperties.Headers["count"].ToString();
                var newCount = int.Parse(countValue) + 1;

                var key = $"retry.{newCount}.after";

                //see if we have exhausted the retry queues
                if (!args.BasicProperties.Headers.TryGetValue(key, out var retryQueue))
                {
                    newCount = -1;
                    retryQueue = -1;
                }
                _logger.LogInformation($"retrying message: {args.RoutingKey}, id: {args.BasicProperties.MessageId}, count: {newCount}");

                args.BasicProperties.Headers["count"] = newCount;
                args.BasicProperties.Headers["retry.in"] = retryQueue;

                _channel.BasicPublish(_busConfiguration.DeadLetterExchangeName, args.RoutingKey, args.BasicProperties, args.Body);

                //remove it from the failed queue
                _channel.BasicAck(args.DeliveryTag, true);
            }

            failedConsumer.Received += FailedEvent;

            _channel.BasicConsume(
                queue: _busConfiguration.FailedQueueName,
                autoAck: false,
                consumer: failedConsumer);

            //Action remove = () => failedConsumer.Received -= ReceivedEvent;
            //return Task.FromResult((IDisposable)new RemoveReceivedEvent(remove, _logger));


            //setup dead-letter
            var deadLetterExchangeName = _busConfiguration.DeadLetterExchangeName;
            _channel.ExchangeDeclare(deadLetterExchangeName, "headers", true);
            QueueDeclareOk deadLetterQueue = _channel.QueueDeclare(_busConfiguration.DeadLetterQueueName, true, false, false);
            _channel.QueueBind(deadLetterQueue.QueueName, deadLetterExchangeName, "", new Dictionary<string, object>()
            {
                { "x-match","any" },
                { "count", -1 },
                { "retry.in", -1 },
                { "deadletter", "true" }
            });

            //setup retries (these will timeout to the retry dlx)
            _channel.ExchangeDeclare(_busConfiguration.RetryExchangeName, "direct", true);
            for (var index = 0; index < _busConfiguration.RetryBackOff.Count; index++)
            {
                var retryInMilliseconds = _busConfiguration.RetryBackOff[index];
                var count = index + 1;

                var properties = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", _busConfiguration.RetryExchangeName },
                    { "x-message-ttl", retryInMilliseconds },
                    { "retry.in", retryInMilliseconds }
                };

                var retryQueue = _channel.QueueDeclare(
                    queue: $"{_busConfiguration.RetryQueuePrefixName}.{retryInMilliseconds}",
                    durable: true,
                    exclusive: false,
                    arguments: properties);

                _channel.QueueBind(
                    retryQueue.QueueName,
                    _busConfiguration.DeadLetterExchangeName,
                    "",
                    new Dictionary<string, object>()
                    {
                        { "x-match","all" },
                        { "retry.in", retryInMilliseconds }
                    });
            }
        }

        public MessagePublishContext<T> CreatePublishContext<T>(string topicName, DestinationType destinationType, Message<T> message)
        {
            var topic = EnsureExchange(topicName, destinationType, true);

            //create the shell of the properties here.
            var properties = _channel.CreateBasicProperties();
            properties.Headers = new Dictionary<string, object>();

            var context = new RabbitMqMessagePublishContext<T>
            {
                Message = message,
                Properties = properties,
            };

            context.PublishAction = () =>
            {
                _channel.BasicPublish(_busConfiguration.RoutingExchangeName, topic, true, properties, context.Body);
                return Task.CompletedTask;
            };

            return context;
        }

        public Task<IDisposable> RegisterConsumer<TMessage>(string topicName, string queueName, SourceType sourceType, Handle<TMessage> handle)
        {
            var queue = EnsureQueue(queueName, topicName, sourceType);
            var consumer = new EventingBasicConsumer(_channel);

            void ReceivedEvent(object sender, BasicDeliverEventArgs args)
            {
                var context = new RabbitMqMessageReceivedContext<TMessage>
                {
                    Payload = args,
                    Acknowledge = () => _channel.BasicAck(args.DeliveryTag, true),
                    NotAcknowledge = () => _channel.BasicNack(args.DeliveryTag, true, true),
                    Reject = () => _channel.BasicReject(args.DeliveryTag, false)
                };

                var task = handle(context);
                task.Wait();
            }

            consumer.Received += ReceivedEvent;
            if(sourceType == SourceType.Stream) _channel.BasicQos(0, 1, false);
            _channel.BasicConsume(
                queue: queue,
                autoAck: false,
                consumer: consumer);

            Action remove = () => consumer.Received -= ReceivedEvent;
            return Task.FromResult((IDisposable)new RemoveReceivedEvent(remove, _logger));
        }

        private string EnsureExchange(string topicName, DestinationType destinationType, bool setupFromPublisher)
        {
            _exchangeLock.GetInsert(
                () =>
                {
                    var done = _exchanges.TryGetValue(topicName, out var fromPublisher);

                    //not setup
                    if (!done)
                    {
                        return false;
                    }

                    //we have something setup, but thats enough as we are only a consumer
                    if (done && !setupFromPublisher)
                    {
                        return true;
                    }

                    //we are a publisher, and the subscriber setup the initial part first
                    if (setupFromPublisher && !fromPublisher)
                    {
                        return false;
                    }

                    //all done
                    return true;
                },

                () =>
                {
                    _channel.ExchangeDeclare(topicName, "fanout", true, false);
                    _channel.ExchangeBind(topicName, _busConfiguration.RoutingExchangeName, topicName);
                    if (_exchanges.ContainsKey(topicName))
                    {
                        _exchanges[topicName] = setupFromPublisher;
                    }
                    else
                    {
                        _exchanges.Add(topicName, setupFromPublisher);
                    }

                    if (destinationType != DestinationType.QueueAndStream) return;

                    //add stream (note this is owned by the producer)
                    var properties = new Dictionary<string, object>
                    {
                        { "x-queue-type", "stream" },
                        { "x-stream-offset", 0 }
                    };

                    var queue = _channel.QueueDeclare(
                        queue: topicName,
                        exclusive: false,
                        durable: true,
                        autoDelete: false,
                        arguments: properties);

                    _channel.QueueBind(queue.QueueName, topicName, topicName);
                });

            return topicName;
        }

        private string EnsureQueue(string queueName, string topicName, SourceType sourceType)
        {
            var destination = sourceType == SourceType.Queue
                ? DestinationType.Queue
                : DestinationType.QueueAndStream;

            EnsureExchange(topicName, destination, false);

            if (sourceType != SourceType.Queue) return topicName;

            //note queues are owned by the consumer
            var properties = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", _busConfiguration.FailedExchangeName },
                { "x-dead-letter-routing-key", queueName },
                { "x-expires", _busConfiguration.ExpireQueueAfter }
            };

            var queue = _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: properties);

            _channel.QueueBind(queue.QueueName, topicName, topicName);
            _channel.QueueBind(queue.QueueName, _busConfiguration.RetryExchangeName, queueName);
            return queue.QueueName;

        }

        public void Dispose()
        {
            _connection?.Dispose();
            _channel?.Dispose();
        }
    }



}