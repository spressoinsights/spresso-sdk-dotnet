using HttpClientFactoryLite;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Auth;


var redisCache = new RedisCache(new OptionsWrapper<RedisCacheOptions>(new RedisCacheOptions
{
    Configuration = "localhost"
}));
    var tokenHandler = new TokenHandler("test123", "secret", new TokenHandlerOptions { Cache = redisCache, AdditionalParameters = "delay=70"});
    var tokenResponse = await tokenHandler.GetTokenAsync();

    Console.WriteLine(tokenResponse.Token);