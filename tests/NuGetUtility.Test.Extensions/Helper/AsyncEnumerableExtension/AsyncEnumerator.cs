// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Test.Extensions.Helper.AsyncEnumerableExtension
{
    internal class AsyncEnumerator<T>(IEnumerator<T> sync) : IAsyncEnumerator<T>
    {
        public ValueTask DisposeAsync()
        {
            sync.Dispose();
            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(sync.MoveNext());
        }

        public T Current => sync.Current;
    }
}
