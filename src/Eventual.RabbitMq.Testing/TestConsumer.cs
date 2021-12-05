namespace Eventual.RabbitMq.Testing
{
    using System.Threading.Tasks;

    internal class TestConsumer<T> : IConsumer<T>
    {
        private readonly MessageState _state;

        public TestConsumer(MessageState state)
        {
            _state = state;
        }

        public Task Handle(Message<T> message)
        {
            _state.Add(message);
            return Task.CompletedTask;
        }
    }
}