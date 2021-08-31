namespace Eventual.Infrastructure
{
    using System;
    using System.Threading;

    public class Lock
    {
        private readonly ReaderWriterLockSlim _internalLock = new ReaderWriterLockSlim();

        public void GetInsert(Func<bool> read, Action update)
        {
            _internalLock.EnterUpgradeableReadLock();
            try
            {
                if (read()) return;
                _internalLock.EnterWriteLock();
                try
                {
                    update();
                }
                finally
                {
                    _internalLock.ExitWriteLock();
                }
            }
            finally
            {
                _internalLock.ExitUpgradeableReadLock();
            }
        }
    }
}