using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Mvc.Testing;
using SpressoAI.Sdk.Core.Auth;
using SpressoAI.Sdk.Core.Connectivity;
using SpressoAI.Sdk.Core.Tests;
using SpressoAI.Sdk.Pricing;
using static System.Net.Mime.MediaTypeNames;

namespace SpressoAI.Sdk.Benchmarks
{
    [MemoryDiagnoser()]
    public class Program
    {
        private readonly TestHttpClientFactory<SpressoAI.MockApi.Program> _httpClientFactory;
        private readonly AuthTokenHandler _authTokenHandler;
        private readonly SpressoHandler _SpressoHandler;

        static void Main(string[] args)
        {
           

           
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        }

        public Program()
        {
            var application = new WebApplicationFactory<SpressoAI.MockApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    // ... Configure test services
                });
            
            _httpClientFactory = new TestHttpClientFactory<SpressoAI.MockApi.Program>(application);
            _authTokenHandler = new AuthTokenHandler("test", "secret", new AuthTokenHandlerOptions
            {
                SpressoHttpClientFactory = new SpressoHttpClientFactory(_httpClientFactory),
                SpressoBaseAuthUrl = "https://localhost",
            });
            _SpressoHandler = new SpressoHandler(_authTokenHandler, new SpressoHandlerOptions
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
            var tokenResponse = await _SpressoHandler.GetPriceAsync(new GetPriceRequest("test", "test", 1.00m, "test", false, "test"));
            return tokenResponse.IsSuccess;
        }


        [Benchmark]
        public async Task<bool> OptimizePriceBatch()
        {
            var tokenResponse = await _SpressoHandler.GetPricesAsync(new GetPricesRequest(new[]
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