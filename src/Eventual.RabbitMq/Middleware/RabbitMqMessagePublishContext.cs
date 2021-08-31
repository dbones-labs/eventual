namespace Eventual.Middleware
{
    using RabbitMQ.Client;

    public class RabbitMqMessagePublishContext<T> : MessagePublishContext<T>
    {
        public byte[] Body { get; set; }
        public IBasicProperties Properties { get; set; }
    }
}