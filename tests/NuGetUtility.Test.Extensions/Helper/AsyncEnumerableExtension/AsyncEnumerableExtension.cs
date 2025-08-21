﻿// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Test.Extensions.Helper.AsyncEnumerableExtension
{
    public static class AsyncEnumerableExtension
    {
        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> synchronous)
        {
            return new AsyncEnumerable<T>(synchronous);
        }

        public static async Task<IEnumerable<T>> Synchronize<T>(this IAsyncEnumerable<T> asyncEnumerable)
        {
            var list = new List<T>();
            await foreach (T? item in asyncEnumerable)
            {
                lock (list)
                {
                    list.Add(item);
                }
            }

            return list;
        }
    }
}
