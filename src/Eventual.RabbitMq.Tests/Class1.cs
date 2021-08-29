using System;

namespace Eventual.RabbitMq.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class Class1
    {
    }




    public static class ApplicationContainer
    {
        public static IServiceCollection Setup(this IServiceCollection serviceCollection,
            Action<IServiceCollection> setup = null,
            Action<IServiceCollection> configureAuditable = null)
        {

            configureAuditable?.Invoke(serviceCollection);
           

            setup?.Invoke(serviceCollection);
            return serviceCollection;
        }

        public static IServiceProvider Build(
            Action<IServiceCollection> setup = null,
            Action<IServiceCollection> configureAuditable = null)
        {
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(configure => configure.AddConsole());
            serviceCollection.Setup(setup, configureAuditable);
            return serviceCollection.BuildServiceProvider();
        }
    }
}
