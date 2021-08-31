namespace Eventual.Infrastructure.BrokerStrategies
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Configuration;


    public class DefaultBrokerStrategy : IBrokerStrategy
    {
        private readonly Dictionary<Type, SourceType> _sourceTypes = new();
        private readonly Lock _sourceLock = new();

        private readonly Dictionary<Type, DestinationType> _destinationTypes = new();
        private readonly Lock _destinationLock = new();


        public DefaultBrokerStrategy(BusConfiguration busConfiguration)
        {
            DefaultProducerStrategy = busConfiguration.PublishToStream
                ? DestinationType.QueueAndStream
                : DestinationType.Queue;

            DefaultConsumerStrategy = busConfiguration.SubscribeFromStream
                ? SourceType.Stream
                : SourceType.Queue;
        }

        public virtual SourceType DefaultConsumerStrategy { get; } = SourceType.Queue;
        public virtual DestinationType DefaultProducerStrategy { get; } = DestinationType.QueueAndStream;

        public virtual List<SourceType> SupportedBrokerTypes { get; } =
            new() { SourceType.Queue, SourceType.Stream }; 
        
        public virtual SourceType GetConsumerBrokerType(Type consumer)
        {
            var result = DefaultConsumerStrategy;
            
            _sourceLock.GetInsert(
                () => _sourceTypes.TryGetValue(consumer, out result),
            
                () =>
                {
                    var broker = consumer.GetCustomAttributes(typeof(ConsumeAttribute), false)
                        .Select(x => (ConsumeAttribute)x)
                        .FirstOrDefault();
                    
                    result = broker is not { From: { } }
                        ? DefaultConsumerStrategy
                        : broker.From.Value;

                    _sourceTypes.Add(consumer, result);
                });

            return result;
        }


        public virtual DestinationType GetProducerBrokerType(Type message)
        {
            var result = DefaultProducerStrategy;

            _destinationLock.GetInsert(
                () => _destinationTypes.TryGetValue(message, out result),

                () =>
                {
                    var broker = message.GetCustomAttributes(typeof(PublishAttribute), false)
                        .Select(x => (PublishAttribute)x)
                        .FirstOrDefault();

                    result = broker is not { To: { } }
                        ? DefaultProducerStrategy
                        : broker.To.Value;

                    _destinationTypes.Add(message, result);
                });

            return result;
        }
    }
}
