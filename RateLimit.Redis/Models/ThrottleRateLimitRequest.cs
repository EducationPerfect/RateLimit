namespace Decepticon.RateLimit
{
    /// <summary>
    /// A throttle request to smooth out the traffic such that it allows requests as long as
    /// there's still quota in the <c>Capacity</c>. The <c>Capacity</c> in the mean time is being refilled
    /// at the at the rate specified by <c>RefillRate</c>. Thus, it averages out the traffic with occational burstiness.
    /// See Token Bucket algorithm.
    /// A request throttling or spike protection system built-in to an enterprise API facade is a good example.
    /// </summary>
    public class ThrottleRateLimitRequest
    {
        /// <summary>
        /// Unique identifer of a domain to rate limit. This can be a client ID, user ID, application ID, etc.
        /// Cannot contain white spaces.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Request refill rate per second. Must be positive.
        /// </summary>
        public int RefillRate { get; set; }

        /// <summary>
        /// Request capacity. Must be positive.
        /// </summary>
        public int Capacity { get; set; }
    }
}
