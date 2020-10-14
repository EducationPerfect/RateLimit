using System;
using Decepticon.RateLimit.Redis.Helpers;
using StackExchange.Redis;

namespace Decepticon.RateLimit
{
    /// <summary>
    /// Provide functions to rate limit. Each function uses different
    /// rate limit algoritms to fit your application's needs.
    /// Currently supports Fixed Window and Token Bucket algorithms.
    /// </summary>
    public class RateLimiter
    {
        private readonly IDatabase _Database;

        /// <summary>
        /// Create a new rate limiter instance from a Redis <see cref="IDatabase"/>
        /// </summary>
        /// <param name="database"></param>
        public RateLimiter(IDatabase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #region Fixed window
        /// <summary>
        /// Validates to check if the request is allowed using the fixed window algorithm.
        /// Use this if you want to allow x requests per day/hour for a specific user, client, or application. 
        /// See <seealso cref="FixedRateLimitRequest"/>. 
        /// Throws <c>ArgumentException</c>
        /// </summary>
        /// <remarks>This will not prevent traffic spikes. If you want to do request throttling to protect your service, use the other overload.</remarks>
        /// <param name="request"></param>
        /// <returns></returns>
        public FixedRateLimitResult Validate(FixedRateLimitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                throw new ArgumentException($"{nameof(request.Key)} identifier must be specified");
            }
            else if (request.Capacity <= 0)
            {
                throw new ArgumentException($"{nameof(request.Capacity)} must be greater than zero");
            }
            else if (request.WindowSize <= 0)
            {
                throw new ArgumentException($"{nameof(request.WindowSize)} must be greater than zero");
            }

            // Convert the window to ticks from seconds
            var windowSizeInTicks = request.WindowSize * TimeSpan.TicksPerSecond;

            // Get current global time from data server like MongoDb, Redis, or SQL server 
            var currentTimeInTicks = GetCurrentTimeInTicks();

            var keys = new RedisKey[]
            {
                $"{Constants.RateLimitCategory}:{Constants.FixedRateCategory}:{request.Key}:count",
                $"{Constants.RateLimitCategory}:{Constants.FixedRateCategory}:{request.Key}:ticks",
            };

            var values = new RedisValue[]
            {
                $"{currentTimeInTicks}",
                $"{windowSizeInTicks}",
                $"{request.Capacity}"
            };

            const string script = @"
                -- Variables
                local tonumber = tonumber
                local currentTimeTicks = tonumber(ARGV[1])
                local windowSizeTicks = tonumber(ARGV[2])
                local capacity = tonumber(ARGV[3])
                local count = 0
                local ticks = currentTimeTicks
                local result = 1
                
                -- Try getting tracking config from existing record
                local countStr = redis.call('GET', KEYS[1])
                if countStr then
                    count = tonumber(countStr)
                end

                local tickStr = redis.call('GET', KEYS[2])
                if tickStr then
                    ticks = tonumber(tickStr)
                end

                -- Increment the counter
                count = count + 1
                
                -- Check the count if request is within the window. Set to denied if greater than the allowe capacity
                if (currentTimeTicks - ticks) < windowSizeTicks then
                    if count > capacity then
                        result = 0
                    end
                -- Otherwise, reset. Count set to 1 because this current request counts.
                else
                    
                    count = 1
                    ticks = currentTimeTicks
                end

                redis.call('SET', KEYS[1], count)
                redis.call('SET', KEYS[2], ticks)

                return result .. ',' .. math.abs(currentTimeTicks - ticks - windowSizeTicks)
            ";

            return ToFixedRateLimitResult(_Database.ScriptEvaluate(script, keys, values).ToString());
        }

        private FixedRateLimitResult ToFixedRateLimitResult(string result)
        {
            var results = result.Split(',');
            var allowed = results[0] != "0";
            return new FixedRateLimitResult
            {
                IsAllowed = allowed,
                ResetAfter = allowed
                    ? 0
                    // Convert to human readable unit
                    : (double.Parse(results[1]) / TimeSpan.TicksPerSecond)
            };
        }
        #endregion

        #region Throttling
        /// <summary>
        /// Validates to check if the request is allowed using the Token Bucket algorithm. 
        /// Use this if you want to prevent spikes by smoothing it out but with some burstiness allowed.
        /// See <seealso cref="ThrottleRateLimitRequest"/>. 
        /// Throws <c>ArgumentException</c>
        /// </summary>
        /// <remarks>If you want to do daily/hour limit, use the other overload.</remarks>
        /// <param name="request"></param>
        /// <returns></returns>
        public ThrottleRateLimitResult Validate(ThrottleRateLimitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                throw new ArgumentException($"{nameof(request.Key)} identifier must be specified");
            }
            else if (request.Capacity <= 0)
            {
                throw new ArgumentException($"{nameof(request.Capacity)} must be greater than zero");
            }
            else if (request.RefillRate <= 0)
            {
                throw new ArgumentException($"{nameof(request.RefillRate)} must be greater than zero");
            }

            // Get current global time from data server like MongoDb, Redis, or SQL server 
            var currentTimeInTicks = GetCurrentTimeInTicks();

            var keys = new RedisKey[]
            {
                $"{Constants.RateLimitCategory}:{Constants.ThrottlingCategory}:{request.Key}:count",
                $"{Constants.RateLimitCategory}:{Constants.ThrottlingCategory}:{request.Key}:ticks",
            };

            var values = new RedisValue[]
            {
                $"{currentTimeInTicks}",
                $"{request.RefillRate}",
                $"{request.Capacity}"
            };

            const string script = @"
                -- Variables
                local tonumber = tonumber
                local currentTimeTicks = tonumber(ARGV[1])
                local refillRate = tonumber(ARGV[2])
                local capacity = tonumber(ARGV[3])
                local count = tonumber(ARGV[3])
                local ticks = currentTimeTicks
                local result = 1
                
                -- Try getting tracking config from existing record
                local countStr = redis.call('GET', KEYS[1])
                if countStr then
                    count = tonumber(countStr)
                end

                local tickStr = redis.call('GET', KEYS[2])
                if tickStr then
                    ticks = tonumber(tickStr)
                end


                -- Refill, take the difference between the last time it refill and now
                -- then divide by ticks in a second
                local tokensToAdd = math.floor((currentTimeTicks - ticks) / 10000000) * refillRate
                count = count + tokensToAdd

                -- Update the timestamp when we have a token refill
                if tokensToAdd > 0 then
                    ticks = currentTimeTicks
                end
                
                -- Tokens are maxed at the capacity
                if count > capacity then
                    count = capacity
                end

                -- Consume a token
                count = count - 1

                -- Determine the outcome
                if count < 0 then
                    result = 0
                    count = 0
                end
                
                redis.call('SET', KEYS[1], count)
                redis.call('SET', KEYS[2], ticks)

                return result .. ',' .. math.abs(10000000 / refillRate)
            ";

            return ToThrottleRateLimitResult(_Database.ScriptEvaluate(script, keys, values).ToString());
        }

        private ThrottleRateLimitResult ToThrottleRateLimitResult(string result)
        {
            var results = result.Split(',');
            var allowed = results[0] != "0";
            return new ThrottleRateLimitResult
            {
                IsAllowed = allowed,
                RetryAfter = allowed
                    ? 0
                    // Convert to human readable unit
                    : (double.Parse(results[1]) / TimeSpan.TicksPerSecond)
            };
        }
        #endregion

        private long GetCurrentTimeInTicks()
        {
            // Call the time function and get the current time in seconds
            var unixTimestampInSeconds = _Database.ScriptEvaluate("return redis.call('TIME')[1]")
                .ToString();

            return DateTimeHelpers.UnixTimeStampToDateTime(double.Parse(unixTimestampInSeconds))
                .Ticks;
        }
    }
}
