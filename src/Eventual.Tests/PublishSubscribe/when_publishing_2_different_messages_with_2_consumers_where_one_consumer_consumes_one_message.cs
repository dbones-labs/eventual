﻿namespace Eventual.Tests.PublishSubscribe
{
    using System.Linq;
    using Machine.Specifications;
    using Messages;
    using PowerAssert;
    using RabbitMq.Testing;

    [Subject("PublishSubscribe")]
    public class when_publishing_2_different_messages_with_2_consumers_where_one_consumer_consumes_one_message
    {
        Establish context = () =>
        {
            host = new Host(new Settings());
            client1 = host.CreateClient();
            client2 = host.CreateClient(transport => transport.Subscribe<Greeting>());
            client3 = host.CreateClient(transport => transport.Subscribe<Wave>());
        };

        Because of = () =>
        {
            client1.Bus.Publish(new Wave { Name = "bones" });
            client1.Bus.Publish(new Greeting { Name = "bob" });

            Wait.For(() => host.State.AllMessages.Count() == 2);
        };

        It should_have_client_2_processing_the_message = () =>
            PAssert.IsTrue(() => client2.State.Messages<Greeting>().Any(x => x.Body.Name == "bob"));

        It should_have_client_3_processing_the_message = () =>
            PAssert.IsTrue(() => client3.State.Messages<Wave>().Any(x => x.Body.Name == "bones"));

        Cleanup after = () => host?.Dispose();

        static Host host;
        static RabbitClient client1;
        static RabbitClient client2;
        static RabbitClient client3;
    }
}