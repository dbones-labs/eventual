namespace Eventual.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Fox.Middleware;
    using Middleware;
    using Middleware.Subscribing;

    public class OpenTelemetryConsumeAction<T> : IConsumeAction<T>
    {
        private readonly Telemetry _telemetry;
        private readonly TelemetryContext _context;

        public OpenTelemetryConsumeAction(Telemetry telemetry, TelemetryContext context)
        {
            _telemetry = telemetry;
            _context = context;
        }

        public async Task Execute(MessageReceivedContext<T> context, Next<MessageReceivedContext<T>> next)
        {
            if (!context.Message.Metadata.TryGetValue(Telemetry.Header, out var traceId))
            {
                traceId = context.Message.CorrelationId;
            }


            var activity = traceId != null
                ? _telemetry.ActivitySource.CreateActivity(typeof(T).FullName, ActivityKind.Consumer, traceId)
                : _telemetry.ActivitySource.CreateActivity(typeof(T).FullName, ActivityKind.Consumer);

            Activity.Current ??= activity;
            _context.CorrelationId = traceId;

            if (activity == null)
            {
                await next(context);
                return;
            }

            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.AddTag("adapter", "rabbitmq");

            using (activity)
            {
                activity.Start();
                try
                {
                    await next(context);
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception e)
                {
                    activity.SetStatus(ActivityStatusCode.Error, e.Message);
                    activity.SetTag("otel.status_code", "ERROR");
                    activity.SetTag("otel.status_description", e.Message);

                    var tags = new List<KeyValuePair<string, object>>();
                    tags.Add(new KeyValuePair<string, object>("message", e.Message));
                    tags.Add(new KeyValuePair<string, object>("stack", e.StackTrace));

                    ActivityEvent @event = new ActivityEvent("error", tags: new ActivityTagsCollection(tags));
                    activity.AddEvent(@event);
                    throw;
                }
            }
        }
    }
}