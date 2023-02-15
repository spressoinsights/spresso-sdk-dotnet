using System.Diagnostics;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Polly.Timeout;
using Spresso.MockApi;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.Core.Connectivity;

namespace Spresso.Sdk.Core.Tests ;

    public class AuthenticationTests
    {
        public AuthenticationTests()
        {
            var application = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    // ... Configure test services
                });

            _httpClientFactory = new TestHttpClientFactory<Program>(application);
        }

        private readonly TestHttpClientFactory<Program> _httpClientFactory;


        private IAuthTokenHandler CreateAuthTokenHandler(HttpStatusCode? statusCode = null, int? delay = null, AuthTokenHandlerOptions options = null)
        {
            options ??= new AuthTokenHandlerOptions();
            options.SpressoHttpClientFactory = new SpressoHttpClientFactory(_httpClientFactory);
            options.SpressoBaseAuthUrl = "https://localhost";

            var additionalParams = "";
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
            options.AdditionalParameters = additionalParams;
            return new AuthTokenHandler("test", "secret", options);
        }

        [Fact]
        public async Task get_token()
        {
            var tokenResponse = await CreateAuthTokenHandler().GetTokenAsync();
            tokenResponse.IsSuccess.Should().BeTrue("because credentials are correct, there were no timeouts, no errors, and the circuit breaker is not open");
            tokenResponse.ExpiresAt.Should().BeAtLeast(TimeSpan.FromHours(23));
            tokenResponse.Token.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task get_token_forbidden()
        {
            var tokenHandler = CreateAuthTokenHandler(HttpStatusCode.Forbidden);
            var tokenResponse = await tokenHandler.GetTokenAsync();
            tokenResponse.IsSuccess.Should().BeFalse("because request has invalid scopes");
            tokenResponse.Error.Should().Be(AuthError.InvalidScopes, "because the server returned a 403 status code");
            tokenResponse.Token.Should().BeNull();
        }


        [Fact]
        public async Task get_token_unauthorized()
        {
            var tokenHandler = CreateAuthTokenHandler(HttpStatusCode.Unauthorized);
            var tokenResponse = await tokenHandler.GetTokenAsync();
            tokenResponse.IsSuccess.Should().BeFalse("because credentials are incorrect");
            tokenResponse.Error.Should().Be(AuthError.InvalidCredentials, "because the server returned a 401 status code");
            tokenResponse.Token.Should().BeNull();
        }


        [Fact]
        public async Task get_token_unauthorized_throw_exception()
        {
            var tokenHandler = CreateAuthTokenHandler(HttpStatusCode.Unauthorized, options: new AuthTokenHandlerOptions
            {
                ThrowOnTokenFailure = true
            });
           
            Func<Task> act = async () => { await tokenHandler.GetTokenAsync(); };
            await act.Should().ThrowAsync<Exception>("because there was an authorization error and throwOnTokenFailure is enabled");
        }

        [Fact]
        public async Task get_token_unknown_error()
        {
            var tokenHandler = CreateAuthTokenHandler(HttpStatusCode.InternalServerError);
            var tokenResponse = await tokenHandler.GetTokenAsync();
            tokenResponse.IsSuccess.Should().BeFalse("because there was an internal server error");
            tokenResponse.Error.Should().Be(AuthError.Unknown, "because the server returned a 500 status code");
            tokenResponse.Token.Should().BeNull();
        }

        [Fact]
        public async Task get_token_unknown_error_throw_exception()
        {
            var tokenHandler = CreateAuthTokenHandler(HttpStatusCode.InternalServerError, options: new AuthTokenHandlerOptions
            {
                ThrowOnTokenFailure = true
            });

            Func<Task> act = async () => { await tokenHandler.GetTokenAsync(); };
            await act.Should().ThrowAsync<Exception>("because there was an error and throwOnTokenFailure is enabled");
        }


    [Fact]
    public async Task get_token_http_timeout()
    {
        var tokenHandler = CreateAuthTokenHandler(delay: 1, options: new AuthTokenHandlerOptions
        {
            HttpTimeout = new TimeSpan(0,0,0,0,500),
            Timeout = new TimeSpan(1,0,0)
        });
        var tokenResponse = await tokenHandler.GetTokenAsync();
        tokenResponse.IsSuccess.Should().BeFalse("because there was an http timeout");
        tokenResponse.Error.Should().Be(AuthError.Timeout, "because there was an http timeout");
        tokenResponse.Token.Should().BeNull();
    }


    [Fact]
    public async Task get_token_timeout()
    {
        var tokenHandler = CreateAuthTokenHandler(delay: 5, options: new AuthTokenHandlerOptions
        {
            Timeout = new TimeSpan(0, 0, 0, 0,200)
        });
        var sw = Stopwatch.StartNew();
        var tokenResponse = await tokenHandler.GetTokenAsync();
        sw.Stop();
        tokenResponse.IsSuccess.Should().BeFalse("because there was an overall timeout");
        tokenResponse.Error.Should().Be(AuthError.Timeout, "because there was an overall timeout");
        tokenResponse.Token.Should().BeNull();
        sw.Elapsed.TotalSeconds.Should().BeLessOrEqualTo(1, "because timeout was set to 200s");
    }

        [Fact]
        public async Task get_token_throw_on_failure_timeout()
        {
            var tokenHandler = CreateAuthTokenHandler(delay: 5, options: new AuthTokenHandlerOptions
            {
                Timeout = new TimeSpan(0, 0, 0, 0, 200),
                ThrowOnTokenFailure = true
            });

            Func<Task> act = async () => { await tokenHandler.GetTokenAsync(); };
            await act.Should().ThrowAsync<TimeoutRejectedException>("because there was a timeout and throwOnTokenFailure is enabled");

        }

        


    [Fact]
    public async Task get_token_trips_circuit_breaker_after_too_many_errors()
    {
        var logger = new MemoryLogger<IAuthTokenHandler>();
        var tokenHandler = CreateAuthTokenHandler(HttpStatusCode.InternalServerError, options:
        new AuthTokenHandlerOptions{
            NumberOfFailuresBeforeTrippingCircuitBreaker = 4,
            Logger = logger
        });

        for (int i = 0; i <= 5; i++)
        {
            var tokenResponse = await tokenHandler.GetTokenAsync();
        }
        logger.Logs.Count(s => s.Equals("Token request failed.  Error (null).  Exception (if applicable): The circuit is now open and is not allowing calls."))
            .Should().Be(2, "because the circuit breaker has been tripped");
    }


       
}