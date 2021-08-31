namespace Eventual.Infrastructure.BrokerStrategies
{
    using System;

    public class ConsumeAttribute : Attribute
    {
        public SourceType? From { get; set; }
    }

    public class PublishAttribute : Attribute
    {
        public DestinationType? To { get; set; }
    }


}