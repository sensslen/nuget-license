// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Test.Extensions.Helper.AsyncEnumerableExtension
{
    internal class AsyncEnumerable<T>(IEnumerable<T> synchronous) : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new())
        {
            return new AsyncEnumerator<T>(synchronous.GetEnumerator());
        }
    }
}
