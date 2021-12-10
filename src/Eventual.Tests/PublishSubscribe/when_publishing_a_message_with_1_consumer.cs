namespace Eventual.Tests.PublishSubscribe
{
    using System.Linq;
    using Machine.Specifications;
    using Messages;
    using PowerAssert;
    using RabbitMq.Testing;

    [Subject("PublishSubscribe")]
    public class when_publishing_a_message_with_1_consumer
    {
        Establish context = () =>
        {
            host = new Host(new Settings());
            client1 = host.CreateClient();
            client2 = host.CreateClient(transport => transport.Subscribe<Wave>());
        };

        Because of = () =>
            client1.Bus.Publish(new Wave { Name = "bones" }).WaitFor(() => client2.State.AllMessages.Any());

        It should_have_client_2_processing_the_message = () => 
            PAssert.IsTrue(() => client2.State.Messages<Wave>().Any(x => x.Body.Name == "bones"));

        Cleanup after = () => host?.Dispose();

        static Host host;
        static RabbitClient client1;
        static RabbitClient client2;
    }
}
