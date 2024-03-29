﻿namespace Eventual.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using BrokerStrategies;
    using Configuration;
    using Middleware;
    using NamingStrategies;
    using Transport;

    public class DefaultSubscriber : IDisposable, ISubscriber
    {
        private readonly INamingStrategy _namingStrategy;
        private readonly IBrokerStrategy _brokerStrategy;
        private readonly IConnection _connection;
        private readonly IDispatcher _dispatcher;
        private readonly BusConfiguration _configuration;

        private readonly Dictionary<Type, IDisposable> _subscriptions = new();

        public DefaultSubscriber(
            INamingStrategy namingStrategy,
            IBrokerStrategy brokerStrategy,
            IConnection connection,
            IDispatcher dispatcher,
            BusConfiguration configuration
        )
        {
            _namingStrategy = namingStrategy;
            _brokerStrategy = brokerStrategy;
            _connection = connection;
            _dispatcher = dispatcher;
            _configuration = configuration;
        }

        public Task<IDisposable> Subscribe(ConsumerSetup setup)
        {
            var topicName = string.IsNullOrWhiteSpace(setup.Topic)
                ? _namingStrategy.GetTopicName(setup.MessageType, _configuration.ServiceName)
                : setup.Topic;

            var queueName = string.IsNullOrWhiteSpace(setup.QueueName)
                ? _namingStrategy.GetQueueName(setup.MessageType, _configuration.ServiceName)
                : setup.QueueName;

            var source = setup.BrokerType ?? _brokerStrategy.GetConsumerBrokerType(setup.MessageType);

            var subscribe = (Task<IDisposable>)this
                .GetType()
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(x => x.Name == nameof(SetupSubscription))
                //.GetMethod("SetupSubscription", BindingFlags.NonPublic)
                ?.MakeGenericMethod(new[] { setup.MessageType })
                .Invoke(this, new object[] { topicName, queueName, source });

            return subscribe;
        }

        public Task<IDisposable> Subscribe<T>()
        {
            var topicName = _namingStrategy.GetTopicName(typeof(T), _configuration.ServiceName);
            var queueName = _namingStrategy.GetQueueName(typeof(T), _configuration.ServiceName);
            var source = _brokerStrategy.GetConsumerBrokerType(typeof(T));

            var consumerTask = SetupSubscription<T>(topicName, queueName, source);

            return consumerTask;
        }

        private Task<IDisposable> SetupSubscription<T>(string topicName, string queueName, SourceType sourceType)
        {
            var consumerTask = _connection.RegisterConsumer<T>(
                topicName,
                queueName,
                sourceType,
                context => _dispatcher.ProcessMessage<T>(context));

            consumerTask.ContinueWith(task => { _subscriptions.Add(typeof(T), task.Result); });
            return consumerTask;
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Value.Dispose();
            }
        }
    }
}