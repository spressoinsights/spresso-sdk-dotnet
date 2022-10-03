using System.Diagnostics;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.PriceOptimizations;

var redisCache = new RedisCache(new OptionsWrapper<RedisCacheOptions>(new RedisCacheOptions
{
    Configuration = "localhost"
}));
 

    var tokenHandler = new TokenHandler("test123", "secret",
        new TokenHandlerOptions
        {
            Cache = redisCache,
            AdditionalParameters = "",
            SpressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL")
        });

    var priceOptimizationHandler = new PriceOptimizationsHandler(tokenHandler, new PriceOptimizationsHandlerOptions
    {
        SpressoBaseUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL"),
        Cache = redisCache,
        AdditionalParameters = ""
    });


    var singleRequest = new GetPriceOptimizationRequest("123", "456", 8.95m);
    var secondRequest = new GetPriceOptimizationRequest("123", "789", 18.95m);
// var sw = Stopwatch.StartNew();
// var response = await priceOptimizationHandler.GetPriceOptimizationAsync(singleRequest);
// sw.Stop();
// Console.WriteLine((response.PriceOptimization?.IsOptimizedPrice ?? false) + " " + sw.Elapsed.TotalSeconds);


    while (true)
    {
    var sw = Stopwatch.StartNew();
        var batchRequest = new GetBatchPriceOptimizationsRequest(new[] { singleRequest, secondRequest });
    
        sw.Start();
        var batchResponse = await priceOptimizationHandler.GetBatchPriceOptimizationsAsync(batchRequest);
        sw.Stop();
        Console.WriteLine(sw.Elapsed.TotalSeconds);
        //Console.ReadKey();
    }


