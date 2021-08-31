namespace Eventual.Configuration
{
    using System;
    using Infrastructure.BrokerStrategies;

    public class ConsumerSetup
    {
        public string QueueName { get; set; } = "";
        public string Topic { get; set; } = "";
        public Type MessageType { get; set; }
        public Type ConsumerType { get; set; }
        public SourceType? BrokerType { get; set; }
    }
}