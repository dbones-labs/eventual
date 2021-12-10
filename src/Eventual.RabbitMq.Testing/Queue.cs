namespace Eventual.RabbitMq.Testing
{
    public class Queue
    {
        public bool AutoDelete { get; set; }
        public bool Durable { get; set; }
        public bool Exclusive { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string State { get; set; }
        public string Vhost { get; set; }
    }
}