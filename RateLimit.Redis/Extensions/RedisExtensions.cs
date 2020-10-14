using Newtonsoft.Json;
using StackExchange.Redis;

namespace Decepticon.RateLimit.Redis.Extensions
{
    /// <summary>
    /// Consists of extension methods for Redis
    /// </summary>
    public static class RedisExtensions
    {
        /// <summary>
        /// Gets value from <paramref name="key"/> and cast it to a specific type.
        /// </summary>
        /// <typeparam name="TDocument"></typeparam>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static TDocument Get<TDocument>(this IDatabase database, string key) where TDocument : class, new()
        {
            var rawValue = database.StringGet(key);
            if (rawValue.HasValue && !rawValue.IsNullOrEmpty)
            {
                return JsonConvert.DeserializeObject<TDocument>(rawValue);
            }

            return (TDocument)null;
        }

        /// <summary>
        /// Sets value for the <paramref name="key"/>
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="database"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void Set<TValue>(this IDatabase database, string key, TValue value) where TValue : class, new()
        {
            if (value != null)
            {
                database.StringSet(key, JsonConvert.SerializeObject(value), null);
            }
            else
            {
                database.StringSet(key, string.Empty, null);
            }
        }
    }
}
