namespace Eventual.RabbitMq.Testing
{
    public class Settings
    {
        public string VHost { get; set; } //= "/";
        public string User { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string Location { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public int AdminPort { get; set; } = 15672;
    }
}