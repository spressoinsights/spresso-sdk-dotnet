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
        
        var host = SetupDependencyInjection(args);

        var logger = host.Services.GetService<ILogger<Program>>();
        logger.LogInformation("Starting Console Test");

        var spressoHandler = host.Services.GetService<ISpressoHandler>();
    
        var singleRequest = new GetPriceRequest("Device-789", "ESPS2", 8.95m,
            userAgent: "Mozilla/5.0 (X11; Linux x86_64; Storebot-Google/1.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36");
        var secondRequest = new GetPriceRequest("Device-789", "EZZL2", 18.95m);

        var singleResponse = await spressoHandler.GetPriceAsync(singleRequest);
        if (singleResponse.IsSuccess)
        {
            PrintPriceOptimization(singleResponse.PriceOptimization!);
        }

        var batchRequest = new GetPricesRequest(new[] { singleRequest, secondRequest }, userAgent: "Gio's Computer");
        var batchResponse = await spressoHandler.GetPricesAsync(batchRequest);
   
        if (batchResponse.IsSuccess)
        {
            foreach (var po in batchResponse.PriceOptimizations)
                PrintPriceOptimization(po);
        }
        else
        {
            foreach (var po in batchResponse.PriceOptimizations)
                PrintPriceOptimization(po);
        }

        Console.WriteLine("Simplified price optimization handler");
        var options = new SpressoHandlerOptions
        {
            SpressoBaseUrl = Environment.GetEnvironmentVariable("SPRESSO_BASE_AUTH_URL")
        };
        var simplifiedSpressoHandler = new SpressoHandler("test123", "secret", options);
        var itemRequest = new GetPriceRequest("Device-789", "AAHV16", 18.95m);
        var simplifiedSingleResponse = await simplifiedSpressoHandler.GetPriceAsync(itemRequest);
        if (simplifiedSingleResponse.IsSuccess)
        {
            PrintPriceOptimization(simplifiedSingleResponse.PriceOptimization!);
        }

        var updateRequest = new CatalogUpdatesRequest(new [] { new CatalogUpdateRequest("foobar-1", "hello world test sku", 43.12m, 1.23m) });
        var catalogUpdateResponse = await simplifiedSpressoHandler.UpdateCatalogAsync(updateRequest);
        if (catalogUpdateResponse.IsSuccess) {
            Console.WriteLine("Update completed successfully");
        } else {
            Console.WriteLine($"Error: {catalogUpdateResponse.Error}");
        }

        var verificationRequest = new PriceVerificationsRequest(new [] { new PriceVerificationRequest("EZZL2", 4.39m, "Device-789") });
        var verificationResponse = await simplifiedSpressoHandler.VerifyPricesAsync(verificationRequest);
        if (verificationResponse.IsSuccess) {
            foreach (var response in verificationResponse.PriceVerifications)
                PrintPriceVerification(response);
        } else {
            Console.WriteLine($"Error: {catalogUpdateResponse.Error}");
        }

        Console.WriteLine("Requests execution finished. Press any key to exit.");
        
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






