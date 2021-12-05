namespace Eventual.RabbitMq.Testing
{
    public class Exchange
    {
        public bool AutoDelete { get; set; }
        public bool Durable { get; set; }
        public bool Internal { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Vhost { get; set; }
    }
}