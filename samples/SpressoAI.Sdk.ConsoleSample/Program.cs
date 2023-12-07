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

        Thread.Sleep(2000);

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

        Thread.Sleep(5 * 60 * 1000);

        start = DateTimeOffset.UtcNow;
        var itemRequest3 = new GetPriceRequest("spressoapidelay-500", "foosku", 18.95m, null, false, "foobarbaz useragent");
        var singleResponse3 = await spressoHandler.GetPriceAsync(itemRequest3);
        if (!singleResponse3.IsSuccess)
        {
            Console.WriteLine($"Error: {singleResponse3.Error}");
        }
        Console.WriteLine($"ItemId: {singleResponse3.PriceOptimization?.ItemId}, Price: {singleResponse3.PriceOptimization?.Price}, Optimized: {singleResponse3.PriceOptimization?.IsPriceOptimized}");
        end = DateTimeOffset.UtcNow;
        Console.WriteLine($"Req3 Time: {end.ToUnixTimeMilliseconds() - start.ToUnixTimeMilliseconds()}ms");

        start = DateTimeOffset.UtcNow;
        var requestArray1 = new List<GetPriceRequest>
        {
            new("spressoapidelay-500", "ZCA4", 18.95m, null, false, "foobarbaz useragent"),
            new("spressoapidelay-500", "AAGE25", 18.95m, null, false, "foobarbaz useragent"),
            new("spressoapidelay-500", "blahblah", 18.95m, null, false, "foobarbaz useragent")
        };
        var batchResponse1 = await spressoHandler.GetPricesAsync(new GetPricesRequest(requestArray1, "foobarbaz useragent"));
        foreach (var response in batchResponse1.PriceOptimizations)
        {
            Console.WriteLine($"ItemId: {response.ItemId}, Price: {response.Price}, Optimized: {response.IsPriceOptimized}");
        }
        end = DateTimeOffset.UtcNow;
        Console.WriteLine($"BatchReq Time: {end.ToUnixTimeMilliseconds() - start.ToUnixTimeMilliseconds()}ms");

        start = DateTimeOffset.UtcNow;
        var requestArray2 = new List<GetPriceRequest>
        {
            new("spressoapidelay-500", "foosku", 18.95m, null, false, "foobarbaz useragent"),
            new("spressoapidelay-500", "barsku", 12.34m, null, false, "foobarbaz useragent"),
            new("spressoapidelay-500", "bazsku", 56.78m, null, false, "foobarbaz useragent")
        };
        var batchResponse2 = await spressoHandler.GetPricesAsync(new GetPricesRequest(requestArray2, "foobarbaz useragent"));
        foreach (var response in batchResponse2.PriceOptimizations)
        {
            Console.WriteLine($"ItemId: {response.ItemId}, Price: {response.Price}, Optimized: {response.IsPriceOptimized}");
        }
        end = DateTimeOffset.UtcNow;
        Console.WriteLine($"BatchReq Time: {end.ToUnixTimeMilliseconds() - start.ToUnixTimeMilliseconds()}ms");

        Console.WriteLine("Done");
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






