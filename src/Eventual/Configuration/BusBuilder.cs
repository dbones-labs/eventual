namespace Eventual.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class BusBuilder
    {
        private Setup _setup;
        private Factory _factory;
        private volatile bool _started = false;

        


        public void SetupContainer(IServiceCollection services, Setup setup)
        {
            _setup = setup;

            _factory = _setup.Transport.GetFactory();

            _factory.RegisterServices(
                services, 
                setup,
                Start);
        }

        public Task Start(IServiceProvider serviceProvider)
        {
            if (_started) return Task.CompletedTask;
            _started = true;

            var tasks = new List<Task>();
            var subscriber = serviceProvider.GetService<ISubscriber>();
            foreach (var consumer in _setup.Consumers)
            {
                var task = subscriber.Subscribe(consumer);
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
            return Task.CompletedTask;
        }
    }
}