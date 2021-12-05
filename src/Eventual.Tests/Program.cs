namespace Eventual.Tests
{
    using System.Linq;
    using Machine.Specifications;
    using PowerAssert;
    using RabbitMq.Testing;

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


    public class when_publishing_a_message_with_2_consumers
    {
        Establish context = () =>
        {
            host = new Host(new Settings());
            client1 = host.CreateClient();
            client2 = host.CreateClient(transport => transport.Subscribe<Wave>());
            client3 = host.CreateClient(transport => transport.Subscribe<Wave>());
        };

        Because of = () =>
            client1.Bus.Publish(new Wave { Name = "bones" }).WaitFor(() => 
                client2.State.AllMessages.Any() && client3.State.AllMessages.Any());

        It should_have_client_2_processing_the_message = () =>
            PAssert.IsTrue(() => client2.State.Messages<Wave>().Any(x => x.Body.Name == "bones"));

        It should_have_client_3_processing_the_message = () =>
            PAssert.IsTrue(() => client3.State.Messages<Wave>().Any(x => x.Body.Name == "bones"));

        Cleanup after = () => host?.Dispose();

        static Host host;
        static RabbitClient client1;
        static RabbitClient client2;
        static RabbitClient client3;
    }



    public class Wave
    {
        public string Name { get; set; }
    }




}
