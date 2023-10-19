using System.Diagnostics;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Polly.Timeout;
using SpressoAI.MockApi;
using SpressoAI.Sdk.Core.Auth;
using SpressoAI.Sdk.Core.Connectivity;
using SpressoAI.Sdk.Core.Tests;


namespace SpressoAI.Sdk.Pricing.Test
{
    public class PriceOptimizationTests
    {
        public PriceOptimizationTests()
        {
            var application = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    // ... Configure test services
                });

            _httpClientFactory = new TestHttpClientFactory<Program>(application);
        }

        private readonly TestHttpClientFactory<Program> _httpClientFactory;

        private IAuthTokenHandler CreateAuthTokenHandler(HttpStatusCode? statusCode = null, int? delay = null, AuthTokenHandlerOptions? options = null)
        {
            options ??= new AuthTokenHandlerOptions();
            options.SpressoHttpClientFactory = new SpressoHttpClientFactory(_httpClientFactory);
            options.SpressoBaseAuthUrl = "https://localhost";
            options.AdditionalParameters = BuildAdditionalParams(statusCode, delay);
            return new AuthTokenHandler("test", "secret", options);
        }

        private static string BuildAdditionalParams(HttpStatusCode? statusCode, int? delay)
        {
            string additionalParams = "";
            if (statusCode.HasValue)
            {
                additionalParams = $"status={(int)statusCode}";
            }
            if (delay.HasValue)
            {
                if (!string.IsNullOrEmpty(additionalParams))
                {
                    additionalParams += "&";
                }

                additionalParams += $"delay={delay}";
            }
            return additionalParams;
        }

        private ISpressoHandler CreateSpressoHandler(HttpStatusCode? statusCode = null, int? delay = null, IAuthTokenHandler? authTokenHandler = null, SpressoHandlerOptions? options = null)
        {
            options ??= new();
            options.SpressoHttpClientFactory = new SpressoHttpClientFactory(_httpClientFactory);
            options.SpressoBaseUrl = "https://localhost";

            if (authTokenHandler == null)
            {
                authTokenHandler = CreateAuthTokenHandler();
            }

            options.AdditionalParameters = BuildAdditionalParams(statusCode, delay);
            return new SpressoHandler(authTokenHandler, options);
        }

        [Fact]
        public async Task get_price_optimization()
        {
            var spressoHandler = CreateSpressoHandler();
            var response = await spressoHandler.GetPriceAsync(new GetPriceRequest("test","1111",9.99m ));
            response.IsSuccess.Should().BeTrue("because the request was successful");
            response.PriceOptimization.Should().NotBeNull("because the request was successful");
            response.PriceOptimization!.IsPriceOptimized.Should().BeTrue("because the item is in a campaign, not an override and not a fallback");
        }

        [Fact]
        public async Task get_price_optimization_override()
        {
            var spressoHandler = CreateSpressoHandler();
            var response = await spressoHandler.GetPriceAsync(new GetPriceRequest("test", "1111", 9.99m, overrideToDefaultPrice: true));
            response.IsSuccess.Should().BeTrue("because the request was successful");
            response.PriceOptimization.Should().NotBeNull("because the request was successful");
            response.PriceOptimization!.IsPriceOptimized.Should().BeFalse("because the override flag was set");
        }

        [Fact]
        public async Task get_price_optimization_uses_default_price_when_price_optimization_api_returns_an_error_response()
        {
            var spressoHandler = CreateSpressoHandler(statusCode: HttpStatusCode.InternalServerError);
            var response = await spressoHandler.GetPriceAsync(new GetPriceRequest("test", "1111", 9.99m));
            response.IsSuccess.Should().BeFalse("because the server returned an 500 status code");
            response.PriceOptimization.Should().NotBeNull("because the request was successful");
            response.PriceOptimization!.IsPriceOptimized.Should().BeFalse("because the fallback price was used");
        }


        [Fact]
        public async Task get_price_optimization_uses_default_price_when_price_optimization_api_times_out()
        {
            var spressoHandler = CreateSpressoHandler(delay: 30, options: new SpressoHandlerOptions { Timeout = new TimeSpan(0, 0, 0, 0, 200) });
            var sw = Stopwatch.StartNew();
            var response = await spressoHandler.GetPriceAsync(new GetPriceRequest("test", "1111", 9.99m));
            sw.Stop();
            response.IsSuccess.Should().BeFalse("because the server response exceeded the set timeout");
            response.PriceOptimization.Should().NotBeNull("because the request was successful");
            response.PriceOptimization!.IsPriceOptimized.Should().BeFalse("because the fallback price was used");
            sw.Elapsed.Should().BeLessThan(new TimeSpan(0, 0, 0, 0, 1000), "because the request should have timed out at 200ms");
        }

        [Fact]
        public async Task get_price_optimization_throws_when_price_optimization_api_times_out_and_throw_on_failure_is_enabled()
        {
            var spressoHandler = CreateSpressoHandler(delay: 30, options: new SpressoHandlerOptions { Timeout = new TimeSpan(0, 0, 0, 0, 200), ThrowOnFailure = true });
            Func<Task> act = async () => await spressoHandler.GetPriceAsync(new GetPriceRequest("test", "1111", 9.99m));
            await act.Should().ThrowAsync<TimeoutRejectedException>("because the server response exceeded the set timeout");
        }



        [Fact]
        public async Task get_price_optimization_uses_default_price_when_token_cannot_be_fetched()
        {
            var authTokenHandler = CreateAuthTokenHandler(statusCode: HttpStatusCode.InternalServerError);
            var spressoHandler = CreateSpressoHandler(authTokenHandler: authTokenHandler);
            var response = await spressoHandler.GetPriceAsync(new GetPriceRequest("test", "1111", 9.99m));
            response.IsSuccess.Should().BeFalse("because the server returned an 500 status code when attempting to fetch a token");
            response.PriceOptimization.Should().NotBeNull("because the request was successful");
            response.PriceOptimization!.IsPriceOptimized.Should().BeFalse("because the fallback price was used");
        }


        [Fact]
        public async Task get_price_optimization_throws_exception_when_token_cannot_be_fetched_and_throw_on_failure_enabled()
        {
            var authTokenHandler = CreateAuthTokenHandler(statusCode: HttpStatusCode.InternalServerError, options: new AuthTokenHandlerOptions { ThrowOnTokenFailure = true });
            var spressoHandler = CreateSpressoHandler(authTokenHandler: authTokenHandler, options: new SpressoHandlerOptions { ThrowOnFailure = true });
            Func<Task> act = async () => await spressoHandler.GetPriceAsync(new GetPriceRequest("test", "1111", 9.99m));
            await act.Should().ThrowAsync<Exception>("because the server returned an 500 status code when attempting to fetch a token");
        }

        [Fact]
        public async Task get_price_optimization_uses_default_price_when_token_request_times_out()
        {
            var authTokenHandler = CreateAuthTokenHandler(delay: 30, options: new AuthTokenHandlerOptions { Timeout = new TimeSpan(0, 0, 0, 0, 200) });
            var spressoHandler = CreateSpressoHandler(authTokenHandler: authTokenHandler);
            var sw = Stopwatch.StartNew();
            var response = await spressoHandler.GetPriceAsync(new GetPriceRequest("test", "1111", 9.99m));
            sw.Stop();
            response.IsSuccess.Should().BeFalse("because the server auth response exceeded the set timeout");
            response.PriceOptimization.Should().NotBeNull("because the request was successful");
            response.PriceOptimization!.IsPriceOptimized.Should().BeFalse("because the fallback price was used");
            sw.Elapsed.Should().BeLessThan(new TimeSpan(0, 0, 0, 0, 1000), "because the request should have timed out at 200ms");
        }

        [Fact]
        public async Task get_price_optimization_throws_exception_when_token_request_times_out_and_throw_on_failure_enabled()
        {
            var authTokenHandler = CreateAuthTokenHandler(delay: 30, options: new AuthTokenHandlerOptions { Timeout = new TimeSpan(0, 0, 0, 0, 200) });
            var spressoHandler = CreateSpressoHandler(authTokenHandler: authTokenHandler, options: new SpressoHandlerOptions { ThrowOnFailure = true });
            Func<Task> act = async () => await spressoHandler.GetPriceAsync(new GetPriceRequest("test", "1111", 9.99m));
            await act.Should().ThrowAsync<Exception>("because the server auth response exceeded the set timeout");
        }
        

        [Fact]
        public async Task get_batch_price_optimizations()
        {
            var spressoHandler = CreateSpressoHandler();
            var response = await spressoHandler.GetPricesAsync(new GetPricesRequest( new List<GetPriceRequest>
            {
                new("test", "1111", 9.99m),
                new("test", "2222", 19.99m),
                new("test", "3333", 120.95m),
            }));

            response.IsSuccess.Should().BeTrue("because the request was successful");
            response.PriceOptimizations.Count().Should().Be(3, "because 3 items were requested");
            response.PriceOptimizations.All(x => x.IsPriceOptimized).Should().BeTrue("because all prices are expected to be optimized");
            response.PriceOptimizations.First().ItemId.Should().Be("1111", "because the response should be in the same order as the request");
            response.PriceOptimizations.Last().ItemId.Should().Be("3333", "because the response should be in the same order as the request");
        }

        [Fact]
        public async Task get_batch_price_optimizations_with_some_overrides()
        {
            var spressoHandler = CreateSpressoHandler();
            var response = await spressoHandler.GetPricesAsync(new GetPricesRequest(new List<GetPriceRequest>
            {
                new("test", "1111", 9.99m),
                new("test", "2222", 19.99m, overrideToDefaultPrice: true),
                new("test", "3333", 120.95m),
            }));

            response.IsSuccess.Should().BeTrue("because the request was successful");
            response.PriceOptimizations.All(x => x.IsPriceOptimized).Should().BeFalse("because the second item was overridden");
        }


        [Fact]
        public async Task get_batch_price_optimizations_uses_default_price_when_price_optimization_api_returns_an_error_response()
        {
            var spressoHandler = CreateSpressoHandler(statusCode: HttpStatusCode.InternalServerError);
            var response = await spressoHandler.GetPricesAsync(new GetPricesRequest(new List<GetPriceRequest>
            {
                new("test", "1111", 9.99m),
                new("test", "2222", 19.99m),
                new("test", "3333", 120.95m),
            }));

            response.IsSuccess.Should().BeFalse("because the server returned an 500 status code");
            response.PriceOptimizations.All(x => x.IsPriceOptimized).Should().BeFalse("because the fallback price was used");
        }

        [Fact]
        public async Task get_batch_price_optimizations_uses_default_price_when_price_optimization_api_times_out()
        {
            var spressoHandler = CreateSpressoHandler(delay: 30, options: new SpressoHandlerOptions { Timeout = new TimeSpan(0, 0, 0, 0, 200) });
            var sw = Stopwatch.StartNew();
            var response = await spressoHandler.GetPricesAsync(new GetPricesRequest(new List<GetPriceRequest>
            {
                new("test", "1111", 9.99m),
                new("test", "2222", 19.99m),
                new("test", "3333", 120.95m),
            }));
            sw.Stop();
            response.IsSuccess.Should().BeFalse("because the server response exceeded the set timeout");
            response.PriceOptimizations.All(x => x.IsPriceOptimized).Should().BeFalse("because the fallback price was used");
            sw.Elapsed.Should().BeLessThan(new TimeSpan(0, 0, 0, 0, 1000), "because the request should have timed out at 200ms");
        }

        [Fact]
        public async Task get_batch_price_optimizations_returns_fallback_prices_when_useragent_override_is_detected()
        {
            var spressoHandler = CreateSpressoHandler();
            var response = await spressoHandler.GetPricesAsync(new GetPricesRequest(new List<GetPriceRequest>
                {
                    new("test", "1111", 9.99m),
                    new("test", "2222", 19.99m),
                    new("test", "3333", 120.95m),
                }, "Googlebot"));

            response.IsSuccess.Should().BeTrue("because the request was successful");
            response.PriceOptimizations.All(x => x.IsPriceOptimized).Should().BeFalse("because the user agent was overridden");
        }
    }
}