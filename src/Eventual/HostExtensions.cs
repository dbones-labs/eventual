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
        /// setup eventual
        /// </summary>
        public static IHostBuilder ConfigureEventual(this IHostBuilder builder, Action<Setup> setup) 
        {
            builder.ConfigureServices((ctx, services) =>
            {
                var busBuilder = new BusBuilder();

                var s = new Setup
                {
                    Configuration = ctx.Configuration
                };

                setup?.Invoke(s);
                
                busBuilder.SetupContainer(services, s);
            });

            return builder;
        }
    }

}