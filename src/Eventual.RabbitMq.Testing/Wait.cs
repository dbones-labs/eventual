namespace Eventual.RabbitMq.Testing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Wait
    {
        public static void WaitFor<T>(this Task<T> task, Func<T, bool> criteria)
        { 
            WaitFor(task, criteria, new TimeSpan(0, 0, 5, 0));
        }

        public static void WaitFor<T>(this Task<T> task, Func<T, bool> criteria, TimeSpan timeout)
        {
            var actualTimeout = DateTime.UtcNow.Add(timeout);

            task.Wait(5000);

            while (!criteria(task.Result))
            {
                task.Start();
                task.Wait(5000);

                if (DateTime.UtcNow > actualTimeout)
                {
                    throw new TimeoutException();
                }

                Thread.Sleep(50);
            }
        }

        public static void WaitFor(this Task task, Func<bool> criteria)
        {
            For(criteria);
        }

        public static void WaitFor(this Task task, Func<bool> criteria, TimeSpan timeout)
        {
            For(criteria, timeout);
        }

        public static void For(Func<bool> criteria)
        { 
            For(criteria, new TimeSpan(0, 0, 5, 0));
        }

        public static void For(Func<bool> criteria, TimeSpan timeout)
        {
            var actualTimeout = DateTime.UtcNow.Add(timeout);

            while (!criteria())
            {
                if (DateTime.UtcNow > actualTimeout)
                {
                    throw new TimeoutException();
                }

                Thread.Sleep(50);
            }
        }
    }
}