using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheTowerFarmer
{
    internal class BroadcastChannel<T> : IDisposable
    {
        private TaskCompletionSource<T> _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock _lock = new();
        private bool _completed;

        public Task<T> WaitAsync(CancellationToken token)
        {
            lock (_lock) 
            {
                if (_completed)
                    _waiting.SetException(new InvalidOperationException("Channel is completed"));

                return _waiting.Task.WaitAsync(token);
            }
        }

        public void Publish(T item)
        {
            TaskCompletionSource<T>? currentTcs;
            lock (_lock)
            {
                if (_completed)
                    throw new InvalidOperationException("Channel is completed");

                currentTcs = _waiting;
                _waiting = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            currentTcs.TrySetResult(item);
        }

        public void Complete()
        {
            lock (_lock)
            {
                if (_completed)
                    return;

                _completed = true;
                _waiting.TrySetException(new TaskCanceledException("Channel is completed"));
            }
        }

        public void Dispose() => Complete();
    }
}
