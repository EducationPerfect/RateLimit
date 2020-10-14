namespace Decepticon.RateLimit
{
    /// <summary>
    /// A result for throttle rate limitting
    /// </summary>
    public class ThrottleRateLimitResult
    {
        /// <summary>
        /// Whether the processed request is allowed.
        /// </summary>
        public bool IsAllowed { get; internal set; }

        /// <summary>
        /// Amount of time in seconds before the next request is allowed if the request is denied. 
        /// If the request is allowed, it defaults to <c>0</c>.
        /// </summary>
        public double RetryAfter { get; internal set; }
    }
}
