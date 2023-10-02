using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Mvc.Testing;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.Core.Connectivity;
using Spresso.Sdk.Core.Tests;
using Spresso.Sdk.PriceOptimizations;
using static System.Net.Mime.MediaTypeNames;

namespace Spresso.Sdk.Benchmarks
{
    [MemoryDiagnoser()]
    public class Program
    {
        private readonly TestHttpClientFactory<Spresso.MockApi.Program> _httpClientFactory;
        private readonly AuthTokenHandler _authTokenHandler;
        private readonly PriceOptimizationsHandler _priceOptimizationsHandler;

        static void Main(string[] args)
        {
           

           
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        }

        public Program()
        {
            var application = new WebApplicationFactory<Spresso.MockApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    // ... Configure test services
                });
            
            _httpClientFactory = new TestHttpClientFactory<Spresso.MockApi.Program>(application);
            _authTokenHandler = new AuthTokenHandler("test", "secret", new AuthTokenHandlerOptions
            {
                SpressoHttpClientFactory = new SpressoHttpClientFactory(_httpClientFactory),
                SpressoBaseAuthUrl = "https://localhost",
            });
            _priceOptimizationsHandler = new PriceOptimizationsHandler(_authTokenHandler, new PriceOptimizationsHandlerOptions
            {
                SpressoHttpClientFactory = new SpressoHttpClientFactory(_httpClientFactory),
                SpressoBaseUrl = "https://localhost"
            });
            
        }

        [Benchmark]
        public async Task<bool> GetTokenAsync()
        {
            var tokenResponse = await _authTokenHandler.GetTokenAsync();
            return tokenResponse.IsSuccess;
        }


        [Benchmark]
        public async Task<bool> OptimizePrice()
        {
            var tokenResponse = await _priceOptimizationsHandler.GetPriceAsync(new GetPriceRequest("test", "test", 1.00m, "test", false, "test"));
            return tokenResponse.IsSuccess;
        }


        [Benchmark]
        public async Task<bool> OptimizePriceBatch()
        {
            var tokenResponse = await _priceOptimizationsHandler.GetPricesAsync(new GetPricesRequest(new[]
            {
                new GetPriceRequest("test", "test", 1.00m, "test", false, "test"),
                new GetPriceRequest("test", "test", 1.00m, "test", false, "test"),
                new GetPriceRequest("test", "test", 1.00m, "test", false, "test"),
                new GetPriceRequest("test", "test", 1.00m, "test", false, "test"),
                new GetPriceRequest("test", "test", 1.00m, "test", false, "test")
            }));
            return tokenResponse.IsSuccess;
        }
    }
}