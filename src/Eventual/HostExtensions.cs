namespace Eventual
{
    using System;
    using Configuration;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public static class HostExtensions
    {
        /// <summary>
        /// this allows you to add Eventual directly to your ServiceCollection, we recommend using the <see cref="ConfigureEventual"/>
        /// </summary>
        /// <param name="serviceCollection">service collection to add Eventual too</param>
        /// <param name="configuration">the application setup</param>
        /// <param name="setup">a delegate to configure eventual, the code parts that is</param>
        /// <returns>the service collection so you can curry with</returns>
        public static IServiceCollection AddEventual(
            this IServiceCollection serviceCollection,
            IConfiguration configuration, 
            Action<Setup> setup)
        {
            var busBuilder = new BusBuilder();

            var s = new Setup
            {
                Configuration = configuration
            };

            setup?.Invoke(s);

            busBuilder.SetupContainer(serviceCollection, s);

            return serviceCollection;
        }


        /// <summary>
        /// setup eventual
        /// </summary>
        public static IHostBuilder ConfigureEventual(this IHostBuilder builder, Action<Setup> setup) 
        {
            builder.ConfigureServices((ctx, services) =>
            {
                services.AddEventual(ctx.Configuration, setup);
            });

            return builder;
        }
    }

}