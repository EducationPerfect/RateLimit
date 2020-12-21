This library allows you to add rate limiting and request throttling to your applications. Rate limiting defines your clients quota such as daily limit as an example. In addition, request throttling helps protect your system from spikes by smoothing out the traffic. Both algorithms use separate tracking in data store.
This library is under [MIT](https://opensource.org/licenses/MIT) license.

## Quickly why?
- To limit quota used by clients in your APIs/services for your business model
- To protect your application from heavy traffic, abuse, or attacks
- Throttling saves you money! 

## Why use this package?
- Supper lightweight
- Free + opensource
- Easy to use
- Simple, look at the code below
- Compatible with both .NetFramework and .NetCore

## Usages
### Fixed Rate Limitting
Here's an example on how to do a daily limit for each of your clients.
```CSharp
// Create a rate limiter by pass in your Redis IDatabase instance to the constructor
var rateLimiter = new RateLimiter(redisDatabaseFromYourApp);

// Make the request such that each of your clients can only make 100 requests a day
var request = new FixedRateLimitRequest
{
    Key = "some_client_id",
    Capacity = 100,
    WindowSize = 24 * 60 * 60
};

var result = rateLimiter.Validate(request);
if (!result.IsAllowed)
{
    // TODO: Return 429 error
    _logger.Log($"Denied: window resets in {result.ResetAfter} seconds");
}

// Code flow continues
... 
```

### Request Throttling
Here's an example on how to do request throttling to allow each client to make 300 requests per minuite but
allow bursts of 50 requests or fewer as long as there's enough capacity.
```CSharp
// Create a rate limiter by pass in your Redis IDatabase instance to the constructor
var rateLimiter = new RateLimiter(redisDatabaseFromYourApp);

var request = new ThrottleRateLimitRequest
{
    Key = "some_client_id",
    Capacity = 50, // this is your bucket size
    RefillRate = 5 // per second
};

var result = rateLimiter.Validate(request);
if (!result.IsAllowed)
{
    // TODO: Return 429 error
    _logger.Log($"Denied: Retry after {result.ResetAfter} seconds");
}

// Code flow continues
... 
```

## Q&A and Troubleshooting
- Why is it slow? The package doens't maintain any connection. It takes in the instance of the database to store data needed. You have to follow the best practices on how to manage connection for the technologies. For an example, in Redis, you have to create your singleton for your Redis connection.
- How can I inject it? The library is made to be used a at a low level to be compatible with .NetFramework and .NetCore, so it's not specific for any injector so you have to create a repository facade for it.
- How to report a bug? Go to the Github page and do it there.
- What algorithms are they? The rate limit function uses Fixed Window. The throttling uses Token Bucket.