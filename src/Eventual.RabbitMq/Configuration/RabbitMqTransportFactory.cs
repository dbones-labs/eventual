namespace Eventual.Configuration
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.NamingStrategies;
    using Microsoft.Extensions.DependencyInjection;
    using Middleware.Publishing;
    using Middleware.Subscribing;
    using Transport;

    public class RabbitMqTransportFactory : Factory
    {
        public override void RegisterServices(
            IServiceCollection services,
            Setup setup,
            Func<IServiceProvider, Task> startFunc)
        {
            base.RegisterServices(services, setup, startFunc);
            services.AddSingleton<IConnection, RabbitMqConnection>();
            services.AddSingleton<INamingStrategy, RabbitMqNamingStrategy>();
            services.AddSingleton(svc => (RabbitMqBusConfiguration) svc.GetService<BusConfiguration>());

            //middleware
            services.AddTransient(typeof(ReadMessageFromQueueIntoContext<>));
            services.AddTransient(typeof(PrepareMessageContextForPublish<>));
            services.AddTransient(typeof(RabbitMqMessageAck<>));

            setup.PublishContextActions.PrepareMessageContextForPublish = typeof(PrepareMessageContextForPublish<>);
            setup.ReceivedContextActions.ReadMessageFromQueueIntoContextAction = typeof(ReadMessageFromQueueIntoContext<>);
            setup.ReceivedContextActions.DeadLetterAction = typeof(RabbitMqMessageAck<>);
        }
    }
}