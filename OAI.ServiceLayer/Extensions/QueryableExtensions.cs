using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OAI.ServiceLayer.Extensions
{
    /// <summary>
    /// Extension methods for working with async enumerable results
    /// </summary>
    public static class QueryableExtensions
    {
        /// <summary>
        /// Converts Task<IEnumerable<T>> to List asynchronously
        /// </summary>
        public static async Task<List<T>> ToListAsync<T>(this Task<IEnumerable<T>> source)
        {
            var result = await source;
            return result.ToList();
        }

        /// <summary>
        /// Gets the first element or default from Task<IEnumerable<T>>
        /// </summary>
        public static async Task<T?> FirstOrDefaultAsync<T>(this Task<IEnumerable<T>> source)
        {
            var result = await source;
            return result.FirstOrDefault();
        }

        /// <summary>
        /// Gets the first element or default with predicate from Task<IEnumerable<T>>
        /// </summary>
        public static async Task<T?> FirstOrDefaultAsync<T>(this Task<IEnumerable<T>> source, Func<T, bool> predicate)
        {
            var result = await source;
            return result.FirstOrDefault(predicate);
        }

        /// <summary>
        /// Orders the results in descending order
        /// </summary>
        public static async Task<IOrderedEnumerable<T>> OrderByDescendingAsync<T, TKey>(
            this Task<IEnumerable<T>> source, 
            Func<T, TKey> keySelector)
        {
            var result = await source;
            return result.OrderByDescending(keySelector);
        }

        /// <summary>
        /// Counts elements in the result
        /// </summary>
        public static async Task<int> CountAsync<T>(this Task<IEnumerable<T>> source)
        {
            var result = await source;
            return result.Count();
        }

        /// <summary>
        /// Checks if any elements exist
        /// </summary>
        public static async Task<bool> AnyAsync<T>(this Task<IEnumerable<T>> source)
        {
            var result = await source;
            return result.Any();
        }

        /// <summary>
        /// Checks if any elements match the predicate
        /// </summary>
        public static async Task<bool> AnyAsync<T>(this Task<IEnumerable<T>> source, Func<T, bool> predicate)
        {
            var result = await source;
            return result.Any(predicate);
        }
    }
}