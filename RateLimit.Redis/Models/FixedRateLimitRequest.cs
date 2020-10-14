namespace Decepticon.RateLimit
{
    /// <summary>
    /// A fixed window request to allow a number requests for a specific period of time.
    /// If the period expires, the window resets. Daily limit is a good example.
    /// </summary>
    public class FixedRateLimitRequest
    {
        /// <summary>
        /// Unique identifer of a domain to rate limit. This can be a client ID, user ID, application ID, etc.
        /// Cannot contain white spaces.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Window size in seconds. Must be positive.
        /// </summary>
        public int WindowSize { get; set; }

        /// <summary>
        /// Capacity allowed in the period specified by <see cref="WindowSize" />. Must be positive.
        /// </summary>
        public int Capacity { get; set; }
    }
}
