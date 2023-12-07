using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpressoAI.Sdk.Core.Auth;
using SpressoAI.Sdk.Pricing;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var spressoHandler = new SpressoHandler("JPgYKJF9JlXFPOrzbNox7WICpn4i7eWv", "SM4BfSMnP8FNgRRCtsSaWQsb0KpEXv4i65jRYxi2AOiVhpJpi7e0_qTFjtlSyB2U");
        var itemRequest = new GetPriceRequest("Device-123", "AAGE25", 18.95m, null, false, "foobarbaz useragent");
        var singleResponse = await spressoHandler.GetPriceAsync(itemRequest);
        if (!singleResponse.IsSuccess)
        {
            Console.WriteLine($"Error: {singleResponse.Error}");
        }
        Console.WriteLine($"ItemId: {singleResponse.PriceOptimization?.ItemId}, Price: {singleResponse.PriceOptimization?.Price}, Optimized: {singleResponse.PriceOptimization?.IsPriceOptimized}");

        var start = DateTimeOffset.UtcNow;
        var itemRequest2 = new GetPriceRequest("spressoapidelay-500", "AAGE25", 18.95m, null, false, "foobarbaz useragent");
        var singleResponse2 = await spressoHandler.GetPriceAsync(itemRequest2);
        if (!singleResponse2.IsSuccess)
        {
            Console.WriteLine($"Error: {singleResponse2.Error}");
        }
        Console.WriteLine($"ItemId: {singleResponse2.PriceOptimization?.ItemId}, Price: {singleResponse2.PriceOptimization?.Price}, Optimized: {singleResponse2.PriceOptimization?.IsPriceOptimized}");
        var end = DateTimeOffset.UtcNow;
        Console.WriteLine($"Req2 Time: {end.ToUnixTimeMilliseconds() - start.ToUnixTimeMilliseconds()}ms");

        Console.WriteLine("Done");
        Thread.Sleep(2000);
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
                    AdditionalParameters = "",
                    SpressoBaseAuthUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL"),
                    Logger = sp.GetService<ILogger<IAuthTokenHandler>>(),
                    Timeout = new TimeSpan(0, 0, 179),
                    HttpTimeout = new TimeSpan(0, 0, 90)
                });
                services.AddSingleton<IAuthTokenHandler, AuthTokenHandler>(
                    sp => new AuthTokenHandler("test123", "secret", sp.GetRequiredService<AuthTokenHandlerOptions>()));
                services.AddSingleton(
                    sp =>
                        new SpressoHandlerOptions
                        {
                            SpressoBaseUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL"),
                            AdditionalParameters = "",
                            Timeout = new TimeSpan(0, 0, 179),
                            HttpTimeout = new TimeSpan(0,0,90),
                            Logger = sp.GetService<ILogger<ISpressoHandler>>()
                        });
                services.AddSingleton<ISpressoHandler, SpressoHandler>();
            })
            .Build();
        return host1;
    }

    private static void PrintPriceOptimization(PriceOptimization priceOptimization)
    {
        Console.WriteLine($"Device: {priceOptimization.DeviceId}, ItemId: {priceOptimization.ItemId}, Price: {priceOptimization.Price}, IsPriceOptimized: {priceOptimization.IsPriceOptimized}");
    }

    private static void PrintPriceVerification(PriceVerification priceVerification)
    {
        Console.WriteLine($"ItemId: {priceVerification.ItemId}, Price: {priceVerification.Price}, Status: {priceVerification.PriceStatus}");
    }
}






