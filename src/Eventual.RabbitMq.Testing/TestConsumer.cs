namespace Eventual.RabbitMq.Testing
{
    using System.Threading.Tasks;

    internal class TestConsumer<T> : IConsumer<T>
    {
        private readonly ClientMessageState _state;

        public TestConsumer(ClientMessageState state)
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