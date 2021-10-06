namespace Eventual.Middleware.Subscribing
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Fox.Middleware;
    using Microsoft.Extensions.Logging;
    using Tracing;

    public class LogReceivedMessage<T> : IConsumeAction<T>
    {
        private readonly TelemetryContext _telemetryContext;
        private readonly ILogger<T> _logger;

        public LogReceivedMessage(TelemetryContext telemetryContext,  ILogger<T> logger)
        {
            _telemetryContext = telemetryContext;
            _logger = logger;
        }

        public async Task Execute(MessageReceivedContext<T> context, Next<MessageReceivedContext<T>> next)
        {
            var id = _telemetryContext.OpenTelemetryTraceId ?? context.Message.Id;
            var messageType = typeof(T).FullName;
            using (var scope = _logger.BeginScope(id))
            {
                _logger.LogDebug($"Receiving message {messageType}");
                try
                {
                    await next(context);
                    _logger.LogDebug($"Received message {messageType}");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to process {messageType}");
                    throw;
                }
            }
        }
    }
}