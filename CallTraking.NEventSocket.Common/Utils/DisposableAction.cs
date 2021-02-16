using CallTraking.NEventSocket.Common.Utils.Extensions;
using System;

namespace CallTraking.NEventSocket.Common.Utils
{
    internal class DisposableAction : IDisposable
    {
        private readonly InterlockedBoolean disposed = new InterlockedBoolean();

        private readonly Action onDispose;

        public DisposableAction(Action onDispose = null)
        {
            this.onDispose = onDispose;
        }

        public void Dispose()
        {
            if (disposed != null && !disposed.EnsureCalledOnce())
            {
                if (onDispose != null)
                {
                    onDispose();
                }
            }
        }
    }
}
