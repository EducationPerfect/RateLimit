namespace Decepticon.RateLimit
{
    /// <summary>
    /// A result for fixed window rate limitting
    /// </summary>
    public class FixedRateLimitResult
    {
        /// <summary>
        /// Whether the processed request is allowed.
        /// </summary>
        public bool IsAllowed { get; internal set; }

        /// <summary>
        /// Amount of time the window resets in seconds if the request is denied. 
        /// If the request is allowed, it's set to <c>0</c>.
        /// </summary>
        public double ResetAfter { get; internal set; }
    }
}
