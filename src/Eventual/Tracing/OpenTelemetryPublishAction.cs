namespace Eventual.Tracing
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Fox.Middleware;
    using Middleware;
    using Middleware.Publishing;

    public class OpenTelemetryPublishAction<T> : IPublishAction<T>
    {
        private readonly Telemetry _telemetry;
        private readonly TelemetryContext _context;

        public OpenTelemetryPublishAction(Telemetry telemetry, TelemetryContext context)
        {
            _telemetry = telemetry;
            _context = context;
        }

        public async Task Execute(MessagePublishContext<T> context, Next<MessagePublishContext<T>> next)
        {
            string parentId;
            var parent = Activity.Current;

            if (parent != null && !string.IsNullOrEmpty(parent.Id) && parent.IdFormat == ActivityIdFormat.W3C)
            {
                parentId = parent.Id;
            }
            else
            {
                parentId = _context?.CorrelationId;
            }

            var activity = parentId != null
                ? _telemetry.ActivitySource.CreateActivity(typeof(T).FullName, ActivityKind.Producer, parentId)
                : _telemetry.ActivitySource.CreateActivity(typeof(T).FullName, ActivityKind.Producer);

            if (activity == null)
            {
                await next(context);
                return;
            }

            activity.SetIdFormat(ActivityIdFormat.W3C);

            using (activity)
            {
                activity.Start();
                context.Message.CorrelationId = activity.Id;
                if (!context.Message.Metadata.ContainsKey(Telemetry.Header)) context.Message.Metadata.Add(Telemetry.Header, activity.Id);
                await next(context);
            }
        }
    }
}