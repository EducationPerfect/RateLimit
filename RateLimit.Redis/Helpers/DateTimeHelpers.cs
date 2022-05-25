using System;

namespace Decepticon.RateLimit.Redis.Helpers
{
    /// <summary>
    /// Consits of date time helper functions
    /// </summary>
    internal static class DateTimeHelpers
    {
        private static readonly DateTime _Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Converts a UNIX timestamp to <see cref="DateTime"/>
        /// </summary>
        /// <param name="unixTimeStampInSeconds"></param>
        /// <returns></returns>
        public static DateTime UnixTimeStampToDateTime(double unixTimeStampInSeconds)
        {
            // Unix timestamp is seconds past epoch
            return _Epoch.AddSeconds(unixTimeStampInSeconds).ToUniversalTime();
        }
    }
}
