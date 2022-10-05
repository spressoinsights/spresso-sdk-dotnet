using System.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.PriceOptimizations;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddLogging(c => c.AddSystemdConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton(o => new RedisCacheOptions { Configuration = "localhost" });
        services.AddSingleton<IDistributedCache, RedisCache>(sp => new RedisCache(sp.GetRequiredService<RedisCacheOptions>()));
        services.AddSingleton(sp => new TokenHandlerOptions
        {
            Cache = sp.GetRequiredService<IDistributedCache>(),
            AdditionalParameters = "",
            SpressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL"),
            Logger = sp.GetService<ILogger<ITokenHandler>>()
        });
        services.AddSingleton<ITokenHandler, TokenHandler>(sp => new TokenHandler("test123", "secret", sp.GetRequiredService<TokenHandlerOptions>()));
        services.AddSingleton(
            sp =>
                new PriceOptimizationsHandlerOptions
                {
                    SpressoBaseUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL"),
                    DistributedCache = sp.GetRequiredService<IDistributedCache>(),
                    AdditionalParameters = "",
                    Timeout = new TimeSpan(1, 0, 0),
                    Logger = sp.GetService<ILogger<IPriceOptimizationHandler>>()
                });
        services.AddSingleton<IPriceOptimizationHandler, PriceOptimizationsHandler>();
    })
    .Build();

    var logger = host.Services.GetService<ILogger<Program>>();
    logger.LogInformation("Starting Console Test");

    var priceOptimizationHandler = host.Services.GetService<IPriceOptimizationHandler>();


    var singleRequest = new GetPriceOptimizationRequest("123", "456", 8.95m,
        userAgent: "Mozilla/5.0 (X11; Linux x86_64; Storebot-Google/1.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36");
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

        break;
    }

    // var overrides = await priceOptimizationHandler.GetPriceOptimizationsUserAgentOverridesAsync(default);
    // overrides = await priceOptimizationHandler.GetPriceOptimizationsUserAgentOverridesAsync(default);


    Console.ReadKey();