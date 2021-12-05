namespace Eventual.RabbitMq.Testing
{
    using System;
    using Configuration;

    public class SetupWrapper
    {
        private readonly Setup _setup;

        public SetupWrapper(Setup setup)
        {
            _setup = setup;
        }

        //public void Subscribe(Type consumer, Action<ConsumerSetup> conf = null)
        //{
        //    _setup.Subscribe(consumer, conf);
        //}

        //public void Subscribe(ConsumerSetup setup)
        //{
        //    _setup.Subscribe(setup);
        //}

        public void Subscribe<T>(Action<ConsumerSetup> conf = null) where T : class
        {
            _setup.Subscribe<TestConsumer<T>>(conf);
        }
    }
}