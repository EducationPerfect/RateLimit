using StackExchange.Redis;

namespace Decepticon.RateLimit.Redis.Tests
{
    public static class RedisDatabaseFactory
    {
        private static IDatabase _Database;

        public static IDatabase GetInstance()
        {
            if (_Database == null)
            {
                var connection = ConnectionMultiplexer.Connect("localhost");
                _Database = connection.GetDatabase();
            }

            return _Database;
        }
    }
}
