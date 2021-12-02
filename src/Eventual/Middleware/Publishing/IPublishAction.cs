namespace Eventual.Middleware.Publishing
{
    using Pipes;

    public interface IPublishAction<T> : IAction<MessagePublishContext<T>> { }
}