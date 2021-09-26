namespace Eventual.Tracing
{
    public class TelemetryContext
    {
        public string OpenTelemetryTraceId { get; set; }
        public string CorrelationId { get; set; }
    }
}