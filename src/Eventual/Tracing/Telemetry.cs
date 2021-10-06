namespace Eventual.Tracing
{
    using System.Diagnostics;
    using System;

    /// <summary>
    /// this is where we control the <see cref="ActivitySource"/>
    /// </summary>
    /// <remarks>
    /// https://github.com/open-telemetry/opentelemetry-dotnet/issues/947
    /// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activity?view=net-5.0
    /// https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Api/README.md
    /// https://docs.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-collection-walkthroughs
    /// https://www.mytechramblings.com/posts/getting-started-with-opentelemetry-and-dotnet-core/
    /// </remarks>
    public class Telemetry : IDisposable
    {
        public Telemetry()
        {
            ActivitySource = new ActivitySource(Name);
        }

        public static string Header = "open.telemetry";
        public static string Name = "eventual.opentelemetry";

        public ActivitySource ActivitySource { get; }

        public void Dispose()
        {
            ActivitySource?.Dispose();
        }
    }
}
