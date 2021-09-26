namespace Eventual
{
    using System;
    using System.Collections.Generic;

    public class Message<T>
    {
        public Message(T body) : this()
        {
            Body = body;
        }

        public Message()
        {
            DateTime = DateTime.UtcNow;
            Id = Guid.NewGuid().ToString("D");
            Metadata = new Dictionary<string, string>();
        }

        public DateTime DateTime { get; set; }

        /// <summary>
        /// a unique Id for this message instance
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// a user defined correlation id. please try to embrace the <see cref="OpenTelemetryTraceId"/>
        /// </summary>
        public string CorrelationId { get; set; }
        
        /// <summary>
        /// the open telemetry trace id, W3C format (this is normally generated from a parent context)
        /// </summary>
        public string OpenTelemetryTraceId { get; set; }

        /// <summary>
        /// any meta data (headers) that should accompany this message for down stream subscribers to process.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }


        /// <summary>
        /// main payload to be published
        /// </summary>
        public T Body { get; set; }
    }
}