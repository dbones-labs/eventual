namespace Eventual.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Machine.Specifications;
    using Messages;
    using PowerAssert;
    using RabbitMq.Testing;

    [Subject("Routing")]
    public class when_publishing_a_message_with_no_consumers
    {
        Establish context = () =>
        {
            host = new Host(new Settings());
            client1 = host.CreateClient();
        };

        Because of = async () =>
        {
            await client1.Bus.Publish(new Wave { Name = "bones" });
            //host.GetExchanges().WaitFor(x => x.Any(x=> x.Name.StartsWith("eventual")));
            Wait.For(() =>
            {
                var task = host.GetExchanges();
                var result = task.Result.Any(x => x.Name.StartsWith("eventual"));
                return result;
            });

            exchanges = host.GetExchanges().Result;
            queues = host.GetQueues().Result;
        };

        It should_not_create_any_queues = () =>
            PAssert.IsTrue(() => queues.All(x => x.Name.StartsWith("queue.")));
        
        It should_create_an_exchange = () =>
            PAssert.IsTrue(() => exchanges.Any(x => x.Name == "eventual.tests.messages.wave"));
       

        Cleanup after = () => host?.Dispose();

        static Host host;
        static RabbitClient client1;
        static List<Exchange> exchanges;
        static List<Queue> queues;
    }
}