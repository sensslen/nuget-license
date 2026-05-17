// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Test.Extensions.Helper.AsyncEnumerableExtension
{
    internal class AsyncEnumerator<T>(IEnumerator<T> sync) : IAsyncEnumerator<T>
    {
        public ValueTask DisposeAsync()
        {
            sync.Dispose();
#if NETFRAMEWORK
            return new ValueTask(Task.CompletedTask);
#else
            return ValueTask.CompletedTask;
#endif
        }

        public ValueTask<bool> MoveNextAsync()
        {
            bool result = sync.MoveNext();
#if NETFRAMEWORK
            return new ValueTask<bool>(Task.FromResult(result));
#else
            return ValueTask.FromResult(result);
#endif
        }

        public T Current => sync.Current;
    }
}
