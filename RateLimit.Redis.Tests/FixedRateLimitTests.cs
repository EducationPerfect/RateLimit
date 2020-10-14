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
    public class FixedRateLimitTests
    {
        private readonly ITestOutputHelper _Output;
        private readonly Stopwatch _Stopwatch;
        private readonly RateLimiter _RateLimiter = new RateLimiter(RedisDatabaseFactory.GetInstance());
        private static readonly List<FixedRateLimitResult> _Results = new List<FixedRateLimitResult>();

        public FixedRateLimitTests(ITestOutputHelper output)
        {
            _Output = output;
            _Stopwatch = new Stopwatch();
        }

        [Fact]
        public void Validate_BasicCheck_Allow()
        {
            // Arrange
            var request = new FixedRateLimitRequest
            {
                Key = "Test1",
                Capacity = 1,
                WindowSize = 1
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
            var request = new FixedRateLimitRequest
            {
                Key = "Test2",
                Capacity = 1,
                WindowSize = 1
            };

            var results = new List<FixedRateLimitResult>();

            // Act
            for (var count = 0; count < numRequest; count++)
            {
                _Stopwatch.Restart();
                var result = _RateLimiter.Validate(request);
                _Output.WriteLine($"{_Stopwatch.Elapsed} allow:{result.IsAllowed} ResetAfter:{result.ResetAfter}");
                
                results.Add(result);
            }

            // Assert
            // Allow first request
            Assert.True(results.First().IsAllowed);
            Assert.Equal(0, results.First().ResetAfter);

            // Deny the rest
            Assert.Equal(numRequest - 1, results.Count(x => !x.IsAllowed));
            // Check reset after value
            Assert.True(results
                .Where(x => !x.IsAllowed)
                .All(x => x.ResetAfter == request.WindowSize));
        }

        [Fact]
        public void Validate_WindowAfterAnother_AllowAndDenyEachWindow()
        {
            // Arrange
            const int numRequest = 10;
            var request = new FixedRateLimitRequest
            {
                Key = "Test3",
                Capacity = 5,
                WindowSize = 2
            };

            var results = new List<FixedRateLimitResult>();
            _Stopwatch.Restart();

            // Act
            for (var count = 0; count < numRequest; count++)
            {
                var result = _RateLimiter.Validate(request);
                _Output.WriteLine($"{_Stopwatch.Elapsed} allow:{result.IsAllowed} ResetAfter:{result.ResetAfter}");

                results.Add(result);
            }

            // Allow first request
            Assert.Equal(request.Capacity, results.Count(x => x.IsAllowed));

            // Waits for the next window
            results.Clear();
            Thread.Sleep(request.WindowSize * 1000);

            for (var count = 0; count < numRequest; count++)
            {
                var result = _RateLimiter.Validate(request);
                _Output.WriteLine($"{_Stopwatch.Elapsed} allow:{result.IsAllowed} ResetAfter:{result.ResetAfter}");

                results.Add(result);
            }

            // Assert
            // Allow first request
            Assert.Equal(request.Capacity, results.Count(x => x.IsAllowed));
        }

        [Theory]
        [InlineData(null, 1, 1)]
        [InlineData("x", 0, 1)]
        [InlineData("x", 1, 0)]
        public void Validate_InvalidParameters_ThrowException(string key, int capacity, int windowSize)
        {
            // Arrange
            var request = new FixedRateLimitRequest
            {
                Key = key,
                Capacity = capacity,
                WindowSize = windowSize
            };

            // Assert
            Assert.Throws<ArgumentException>(() => _RateLimiter.Validate(request));
        }

        [Fact]
        public void Validate_OverCapacity_DistributedSystem_Deny()
        {
            // Arrange
            var request = new FixedRateLimitRequest
            {
                Key = "Test4",
                Capacity = 20,
                WindowSize = 2
            };

            // Act
            var tasks = new List<Task>();
            for (var count = 0; count < 5; count++)
            {
                var task = Task.Run(() => Execute(request));
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(request.Capacity, _Results.Count(x => x.IsAllowed));
        }

        private void Execute(FixedRateLimitRequest request)
        {
            Thread.CurrentThread.Name = Guid.NewGuid().ToString().Substring(0, 5);
            for (var count = 0; count < 10; count++)
            {
                var result = _RateLimiter.Validate(request);
                _Results.Add(result);
                _Output.WriteLine($"Thread:{Thread.CurrentThread.Name} allow:{result.IsAllowed} ResetAfter:{result.ResetAfter}");
            }
        }
    }
}
