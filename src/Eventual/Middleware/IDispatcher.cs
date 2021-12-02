﻿namespace Eventual.Middleware
{
    using System;
    using System.Threading.Tasks;
    using Pipes;
    using Microsoft.Extensions.DependencyInjection;
    using Publishing;
    using Subscribing;

    public interface IDispatcher
    {
        Task ProcessMessage<T>(MessageReceivedContext<T> receivedContext);
        Task ProcessMessage<T>(MessagePublishContext<T> publishContext);
    }

    public class DefaultDispatcher : IDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task ProcessMessage<T>(MessageReceivedContext<T> receivedContext)
        {
            var middleware = _serviceProvider.GetService<ReceivedMessageMiddleware<T>>();
            return middleware.Execute(_serviceProvider, receivedContext);
        }

        public Task ProcessMessage<T>(MessagePublishContext<T> publishContext)
        {
            var middleware = _serviceProvider.GetService<PublishedMessageMiddleware<T>>();
            return middleware.Execute(_serviceProvider, publishContext);
        }
    }

    
}