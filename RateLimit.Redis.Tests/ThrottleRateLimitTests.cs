using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Decepticon.RateLimit.Redis.Tests
{
    public class ThrottleRateLimitTests
    {
        private readonly ITestOutputHelper _Output;
        private readonly Stopwatch _Stopwatch;
        private readonly RateLimiter _RateLimiter = new RateLimiter(RedisDatabaseFactory.GetInstance());
        private static readonly List<ThrottleRateLimitResult> _Results = new List<ThrottleRateLimitResult>();

        public ThrottleRateLimitTests(ITestOutputHelper output)
        {
            _Output = output;
            _Stopwatch = new Stopwatch();
        }

        [Fact]
        public void Validate_BasicCheck_Allow()
        {
            // Arrange
            var request = new ThrottleRateLimitRequest
            {
                Key = "Test1",
                Capacity = 1,
                RefillRate = 1
            };

            // Act
            var result = _RateLimiter.Validate(request).IsAllowed;

            Assert.True(result);
        }

        [Fact]
        public void Validate_OverCapacity_Deny()
        {
            // Arrange
            const int numRequest = 10;
            var request = new ThrottleRateLimitRequest
            {
                Key = "Test2",
                Capacity = 5,
                RefillRate = 1
            };

            var results = new List<ThrottleRateLimitResult>();

            // Act
            for (var count = 0; count < numRequest; count++)
            {
                _Stopwatch.Restart();
                var result = _RateLimiter.Validate(request);
                _Output.WriteLine($"{_Stopwatch.Elapsed} allow:{result.IsAllowed} RetryAfter:{result.RetryAfter}");
                
                results.Add(result);
            }

            // Assert
            // Allow first 5 requests
            Assert.True(results[0].IsAllowed);
            Assert.True(results[1].IsAllowed);
            Assert.True(results[2].IsAllowed);
            Assert.True(results[3].IsAllowed);
            Assert.True(results[4].IsAllowed);

            // Deny the rest
            Assert.Equal(numRequest - request.Capacity, results.Count(x => !x.IsAllowed));
            // Check reset after value
            Assert.True(results
                .Where(x => !x.IsAllowed)
                .All(x => x.RetryAfter == request.RefillRate));
        }

        [Fact]
        public void Validate_BurstCapacityAfterAnother_AllowAndDenyEachCapacity()
        {
            // Arrange
            const int numRequest = 10;
            var request = new ThrottleRateLimitRequest
            {
                Key = "Test3",
                Capacity = 5,
                RefillRate = 5
            };

            var results = new List<ThrottleRateLimitResult>();

            // Act
            for (var count = 0; count < numRequest; count++)
            {
                _Stopwatch.Restart();
                var result = _RateLimiter.Validate(request);
                _Output.WriteLine($"{_Stopwatch.Elapsed} allow:{result.IsAllowed} RetryAfter:{result.RetryAfter}");

                results.Add(result);
            }

            Assert.Equal(request.Capacity, results.Count(x => x.IsAllowed));

            // Waits for the next window
            results.Clear();
            Thread.Sleep(1000);

            for (var count = 0; count < numRequest; count++)
            {
                _Stopwatch.Restart();
                var result = _RateLimiter.Validate(request);
                _Output.WriteLine($"{_Stopwatch.Elapsed} allow:{result.IsAllowed} RetryAfter:{result.RetryAfter}");

                results.Add(result);
            }

            Assert.Equal(request.Capacity, results.Count(x => x.IsAllowed));
        }

        [Theory]
        [InlineData(null, 1, 1)]
        [InlineData("x", 0, 1)]
        [InlineData("x", 1, 0)]
        public void Validate_InvalidParameters_ThrowException(string key, int capacity, int refillRate)
        {
            // Arrange
            var request = new ThrottleRateLimitRequest
            {
                Key = key,
                Capacity = capacity,
                RefillRate = refillRate
            };

            // Assert
            Assert.Throws<ArgumentException>(() => _RateLimiter.Validate(request));
        }

        [Fact]
        public void Validate_OverCapacity_DistributedSystem_AllowThenDeny()
        {
            // Arrange
            var request = new ThrottleRateLimitRequest
            {
                Key = "Test4",
                Capacity = 20,
                RefillRate = 1
            };

            // Act
            var tasks = new List<Task>();
            for (var count = 0; count < 5; count++)
            {
                var task = Task.Run(() => Execute(request, null));
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.True(_Results.Count(x => x.IsAllowed) > 1);
            Assert.True(_Results.Count(x => !x.IsAllowed) > 1);
        }

        [Fact]
        public void Validate_OverCapacity_DistributedSystem_SustainedRate_Allow()
        {
            // Arrange
            var request = new ThrottleRateLimitRequest
            {
                Key = "Test6",
                Capacity = 20,
                RefillRate = 1
            };

            // Act
            var tasks = new List<Task>();
            for (var count = 0; count < 5; count++)
            {
                var task = Task.Run(() => Execute(request, 1000));
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.True(_Results.Count(x => x.IsAllowed) > 1);
            Assert.True(_Results.Count(x => !x.IsAllowed) > 1);
        }


        [Fact]
        public void Validate_SustainedRate_Allow()
        {
            // Arrange
            const int numRequest = 10;
            var request = new ThrottleRateLimitRequest
            {
                Key = "Test5",
                Capacity = 1,
                RefillRate = 1
            };

            var results = new List<ThrottleRateLimitResult>();

            // Act
            for (var count = 0; count < numRequest; count++)
            {
                _Stopwatch.Restart();
                var result = _RateLimiter.Validate(request);
                _Output.WriteLine($"{_Stopwatch.Elapsed} allow:{result.IsAllowed} RetryAfter:{result.RetryAfter}");

                Thread.Sleep(1000);
                results.Add(result);
            }

            Assert.Equal(numRequest, results.Count(x => x.IsAllowed));
        }

        [Fact]
        public void Validate_AllowSustainedRate_ThenDenyBurst()
        {
            // Arrange
            const int numRequest = 3;
            var request = new ThrottleRateLimitRequest
            {
                Key = "Test6",
                Capacity = 1,
                RefillRate = 1
            };

            var results = new List<ThrottleRateLimitResult>();

            // Act
            for (var count = 0; count < numRequest; count++)
            {
                _Stopwatch.Restart();
                var result = _RateLimiter.Validate(request);
                _Output.WriteLine($"{_Stopwatch.Elapsed} allow:{result.IsAllowed} RetryAfter:{result.RetryAfter}");

                Thread.Sleep(1000);
                results.Add(result);
            }

            Assert.Equal(numRequest, results.Count(x => x.IsAllowed));

            Assert.True(_RateLimiter.Validate(request).IsAllowed);
            Assert.False(_RateLimiter.Validate(request).IsAllowed);
        }


        private void Execute(ThrottleRateLimitRequest request, int? interval)
        {
            Thread.CurrentThread.Name = Guid.NewGuid().ToString().Substring(0, 5);
            for (var count = 0; count < 10; count++)
            {
                var result = _RateLimiter.Validate(request);
                _Results.Add(result);
                _Output.WriteLine($"Thread:{Thread.CurrentThread.Name} allow:{result.IsAllowed} RetryAfter:{result.RetryAfter}");

                if (interval.HasValue)
                {
                    Thread.Sleep(interval.Value);
                }
            }
        }
    }
}
