using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.PriceOptimizations;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = SetupDependencyInjection(args);

        var logger = host.Services.GetService<ILogger<Program>>();
        logger.LogInformation("Starting Console Test");

        var priceOptimizationHandler = host.Services.GetService<IPriceOptimizationHandler>();



        var singleRequest = new GetPriceOptimizationRequest("123", "456", 8.95m,
            userAgent: "Mozilla/5.0 (X11; Linux x86_64; Storebot-Google/1.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36");
        var secondRequest = new GetPriceOptimizationRequest("123", "789", 18.95m);



        var batchRequest = new GetBatchPriceOptimizationsRequest(new[] { singleRequest, secondRequest });
        var batchResponse = await priceOptimizationHandler.GetBatchPriceOptimizationsAsync(batchRequest);


        if (batchResponse.IsSuccess)
        {
            Console.WriteLine("Optimized Prices");
            foreach (var po in batchResponse.PriceOptimizations)
                PrintPriceOptimization(po);
        }
        else
        {
            Console.WriteLine("Could not get default optimized prices.  Using fallback prices");
            foreach (var po in batchResponse.PriceOptimizations)
                PrintPriceOptimization(po);
        }
        Console.ReadKey();
    }

    private static IHost SetupDependencyInjection(string[] strings)
    {
        var host1 = Host.CreateDefaultBuilder(strings)
            .ConfigureServices(services =>
            {
                services.AddLogging(c => c.AddSystemdConsole().SetMinimumLevel(LogLevel.Debug));
                services.AddSingleton(o => new RedisCacheOptions { Configuration = "localhost" });
                services.AddSingleton<IDistributedCache, RedisCache>(sp => new RedisCache(sp.GetRequiredService<RedisCacheOptions>()));
                services.AddSingleton(sp => new AuthTokenHandlerOptions
                {
                    Cache = sp.GetRequiredService<IDistributedCache>(),
                    AdditionalParameters = "",
                    SpressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL"),
                    Logger = sp.GetService<ILogger<IAuthTokenHandler>>()
                });
                services.AddSingleton<IAuthTokenHandler, AuthTokenHandler>(
                    sp => new AuthTokenHandler("test123", "secret", sp.GetRequiredService<AuthTokenHandlerOptions>()));
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
        return host1;
    }

    private static void PrintPriceOptimization(PriceOptimization priceOptimization)
    {
        Console.WriteLine($"Device: {priceOptimization.DeviceId}, Item: {priceOptimization.ItemId}, Price: {priceOptimization.Price}");
    }
}






