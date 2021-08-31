namespace Eventual.Infrastructure.BrokerStrategies
{
    using System;
    using System.Collections.Generic;

    public interface IBrokerStrategy
    {
        SourceType DefaultConsumerStrategy { get; }
        DestinationType DefaultProducerStrategy { get; }
        List<SourceType> SupportedBrokerTypes { get; }

        SourceType GetConsumerBrokerType(Type consumer);
        DestinationType GetProducerBrokerType(Type message);
    }
}