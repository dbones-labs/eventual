namespace Eventual.Tests.Headers
{
    using System.Linq;
    using Machine.Specifications;
    using Messages;
    using PowerAssert;
    using RabbitMq.Testing;

    [Subject("Headers")]
    public class when_publishing_a_message_with_a_custom_correlation_id
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
                CorrelationId = "123"
            }).WaitFor(() => client2.State.AllMessages.Any());


        It should_have_a_message_with_a_correlation_id = () =>
            PAssert.IsTrue(() => client2.State.Messages<Wave>().First().CorrelationId == "123");

        Cleanup after = () => host?.Dispose();

        static Host host;
        static RabbitClient client1;
        static RabbitClient client2;
    }
}