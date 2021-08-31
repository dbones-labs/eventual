namespace Eventual.Configuration
{
    using System.Reflection;

    public abstract class BusConfiguration
    {
        public string ServiceName { get; set; } =
            Assembly.GetEntryAssembly().GetName().Name;


        /// <summary>
        /// all messages will publish to a topic stream by default
        ///
        /// depending on the transport this may still allow queue's to be published too as well
        /// </summary>
        public bool PublishToStream { get; set; }

        /// <summary>
        /// all messages will be subscribed from a stream
        /// </summary>
        public bool SubscribeFromStream { get; set; }
    }
}