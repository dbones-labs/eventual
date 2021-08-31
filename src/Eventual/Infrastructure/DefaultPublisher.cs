namespace Eventual.Infrastructure
{
    using System.Threading.Tasks;
    using BrokerStrategies;
    using Configuration;
    using Middleware;
    using NamingStrategies;
    using Transport;

    public class DefaultPublisher : IPublisher
    {
        private readonly IConnection _connection;
        private readonly INamingStrategy _namingStrategy;
        private readonly IBrokerStrategy _brokerStrategy;
        private readonly IDispatcher _dispatcher;
        private readonly BusConfiguration _configuration;

        public DefaultPublisher(
            IConnection connection,
            INamingStrategy namingStrategy,
            IBrokerStrategy brokerStrategy,
            IDispatcher dispatcher,
            BusConfiguration configuration)
        {
            _connection = connection;
            _namingStrategy = namingStrategy;
            _brokerStrategy = brokerStrategy;
            _dispatcher = dispatcher;
            _configuration = configuration;
        }

        public Task Publish<T>(T body)
        {
            var completeMessage = new Message<T>(body);
            return Publish(completeMessage);
        }

        public Task Publish<T>(Message<T> message)
        {
            var queueName = _namingStrategy.GetTopicName(typeof(T), _configuration.ServiceName);
            var destination = _brokerStrategy.GetProducerBrokerType(typeof(T));
            var context = _connection.CreatePublishContext(queueName, destination, message);
            
            return _dispatcher.ProcessMessage(context);
        }
    }
}