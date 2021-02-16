using System;

namespace CallTraking.NEventSocket.Common.Utils
{
    internal sealed partial class AsyncLock
    {
        private sealed class Releaser : IDisposable
        {
            private readonly AsyncLock _toRelease;

            internal Releaser(AsyncLock toRelease)
            {
                _toRelease = toRelease;
            }

            public void Dispose()
            {
                _toRelease._semaphore.Release();
            }
        }
    }

}
