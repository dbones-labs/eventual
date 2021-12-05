namespace Eventual.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Machine.Specifications;
    using PowerAssert;
    using RabbitMq.Testing;


    public class when_publishing_a_message_to_1_consumer
    {
        Establish context = () =>
        {
            host = new Host(new Settings());
            client1 = host.CreateClient();
            client2 = host.CreateClient(transport => transport.Subscribe<Wave>());
        };

        Because of = () =>
            client1.Bus.Publish(new Wave { Name = "bones" }).WaitFor(() => client2.State.AllMessages.Any());

        It should = () => PAssert.IsTrue(() => client2.State.Messages<Wave>().Any(x => x.Body.Name == "bones"));

        Cleanup after = () => host?.Dispose();

        static Host host;
        static RabbitClient client1;
        static RabbitClient client2;
    }



    public class Wave
    {
        public string Name { get; set; }
    }


}
