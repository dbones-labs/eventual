namespace Eventual.Infrastructure.BrokerStrategies
{
    public enum SourceType
    {
        Queue,
        Stream
    }

    public enum DestinationType
    {
        Queue,
        QueueAndStream
    }
}