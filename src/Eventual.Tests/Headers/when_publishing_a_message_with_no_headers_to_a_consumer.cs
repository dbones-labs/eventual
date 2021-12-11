namespace Eventual.Tests.Headers
{
    using System;
    using System.Linq;
    using Machine.Specifications;
    using Messages;
    using PowerAssert;
    using RabbitMq.Testing;

    [Subject("Headers")]
    public class when_publishing_a_message_with_no_headers_to_a_consumer
    {
        Establish context = () =>
        {
            host = new Host(new Settings());
            client1 = host.CreateClient();
            client2 = host.CreateClient(transport => transport.Subscribe<Wave>());
        };

        Because of = () =>
            client1.Bus.Publish(new Message<Wave>()
            {
                Body = new Wave { Name = "bones" },
            }).WaitFor(() => client2.State.AllMessages.Any());

        It should_have_a_message_with_the_passed_retry_headers = () =>
            PAssert.IsTrue(() => client2.State.Messages<Wave>().First().Metadata.Count == 6);

        It should_have_a_message_with_an_id = () =>
            PAssert.IsTrue(() => !string.IsNullOrWhiteSpace(client2.State.Messages<Wave>().First().Id));

        It should_have_a_message_with_a_date_time = () =>
            PAssert.IsTrue(() => client2.State.Messages<Wave>().First().DateTime != DateTime.MinValue);

        It should_have_a_message_with_a_correlation_id = () =>
            PAssert.IsTrue(() => !string.IsNullOrWhiteSpace(client2.State.Messages<Wave>().First().CorrelationId));

        Cleanup after = () => host?.Dispose();

        static Host host;
        static RabbitClient client1;
        static RabbitClient client2;
    }
}