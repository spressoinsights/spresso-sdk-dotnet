using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Auth;

var redisCache = new RedisCache(new OptionsWrapper<RedisCacheOptions>(new RedisCacheOptions
{
    Configuration = "localhost"
}));

    var tokenHandler = new TokenHandler("test123", "secret", new TokenHandlerOptions { Cache = redisCache, AdditionalParameters = "status=401", SpressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL") });
    for (int i = 0; i < 50; i++)
    {
        
        var tokenResponse = await tokenHandler.GetTokenAsync();

        Console.WriteLine("Token: " + tokenResponse.Token);
    }


Console.ReadKey();