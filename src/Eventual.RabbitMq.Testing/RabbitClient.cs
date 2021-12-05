namespace Eventual.RabbitMq.Testing
{
    using System;
    using System.Net.Http;
    using Configuration;
    using Infrastructure.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class RabbitClient : IDisposable
    {
        private ServiceProvider _container;
        private static int _clientCount = 0;

        public RabbitClient(Settings settings, string vhost, HttpClient client, Action<SetupWrapper> setupAction = null)
        { 
            _clientCount++;


            //a test client of eventual will be used to listen to messages.
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<MessageState>();
            serviceCollection.AddLogging(configure => configure.AddConsole());
            serviceCollection.AddEventual(null, setup =>
            {
                setup.UseTransport<RabbitMq>(mq =>
                {
                    //"amqp://user:pass@hostName:port/vhost"
                    //"amqp://localhost/%2f"
                    mq.BusConfiguration.ConnectionString =
                        $"amqp://{settings.User}:{settings.Password}@{settings.Location}:{settings.Port}/{vhost}";

                    mq.BusConfiguration.ServiceName = $"test-client-{_clientCount}";

                });

                var wrapper = new SetupWrapper(setup);
                setupAction?.Invoke(wrapper);
            });

            _container = serviceCollection.BuildServiceProvider();
            var busService = _container.GetService<IInitBus>();
            busService.Start().Wait(5000);
            Bus = _container.GetService<IBus>();
            State = _container.GetService<MessageState>();
        }

        public IPublisher Bus { get; set; }
        public MessageState State { get; set; }

        

        public void Dispose()
        {
            _container?.Dispose();
        }
    }
}