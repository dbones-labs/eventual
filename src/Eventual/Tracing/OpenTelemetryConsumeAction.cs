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
            var traceId =  context.Message.OpenTelemetryTraceId;
            var activity = traceId != null
                ? _telemetry.ActivitySource.StartActivity(typeof(T).FullName, ActivityKind.Consumer, traceId)
                : _telemetry.ActivitySource.StartActivity(typeof(T).FullName, ActivityKind.Consumer);

            _context.CorrelationId = context.Message.CorrelationId;

            if (activity == null)
            {
                await next(context);
                return;
            }

            //activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.AddTag("adapter", "eventual");
            activity.AddTag("message.id", context.Message.Id);
            activity.AddTag("message.correlation.id", context.Message.CorrelationId);

            Activity.Current = activity;
            _context.OpenTelemetryTraceId = activity?.Id;

            using (activity)
            {
                //activity.Start();
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

                    var tags = new List<KeyValuePair<string, object>>
                    {
                        new("message", e.Message),
                        new("stack", e.StackTrace)
                    };

                    ActivityEvent @event = new ActivityEvent("error", tags: new ActivityTagsCollection(tags));
                    activity.AddEvent(@event);
                    throw;
                }
            }
        }
    }
}