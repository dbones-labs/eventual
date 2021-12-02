namespace Eventual.Middleware.Subscribing
{
    using Pipes;

    public interface IConsumeAction<T> : IAction<MessageReceivedContext<T>> {}
}