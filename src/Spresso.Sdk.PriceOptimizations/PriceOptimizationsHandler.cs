using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using Polly.Timeout;
using Spresso.Sdk.Core.Auth;
using Spresso.Sdk.Core.Connectivity;
using Spresso.Sdk.Core.Resiliency;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandler : IPriceOptimizationHandler
    {
        public enum UserAgentStatus : byte
        {
            Active = 0,
            Disabled = 1,
            Deleted = 2
        }

        private const string PriceOptimizationsEndpoint = "/pim/v1/prices";
        private const int MaxRequestSize = 500;
        private readonly string _additionalParameters;
        private readonly IAuthTokenHandler _authTokenHandler;
        private readonly string _baseUrl;
        private readonly IAsyncPolicy<GetPriceResponse> _getPriceOptimizationPolicy;
        private readonly IAsyncPolicy<GetPricesResponse> _getPriceOptimizationsBatchPolicy;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _httpTimeout;

        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        private readonly ILogger<IPriceOptimizationHandler> _logger;

        public PriceOptimizationsHandler(IAuthTokenHandler authTokenHandler,
            PriceOptimizationsHandlerOptions? options = null)
        {
            options ??= new PriceOptimizationsHandlerOptions();
            _logger = options.Logger;
            _authTokenHandler = authTokenHandler;
            _httpClientFactory = options.SpressoHttpClientFactory;
            _baseUrl = options.SpressoBaseUrl;
            _httpTimeout = options.HttpTimeout;
            _additionalParameters = options.AdditionalParameters;
            _getPriceOptimizationPolicy = CreatePriceOptimizationResiliencyPolicy(options);
            _getPriceOptimizationsBatchPolicy = CreatePriceOptimizationsBatchResiliencyPolicy(options);
        }

        /// <inheritdoc cref="IPriceOptimizationHandler.GetPriceAsync" />
        public async Task<GetPriceResponse> GetPriceAsync(GetPriceRequest request,
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@GetPriceAsync@@";
            
            var executionResult = await _getPriceOptimizationPolicy.ExecuteAsync(async () =>
            {
                if (!string.IsNullOrEmpty(request.UserAgent))
                {
                    var userAgentOverridesResponse =
                        await GetPriceOptimizationsUserAgentOverridesAsync(cancellationToken);
                    if (userAgentOverridesResponse.IsSuccess)
                    {
                        if (userAgentOverridesResponse.UserAgentRegexes.Any(regex => regex.IsMatch(request.UserAgent)))
                        {
                            _logger.LogDebug(
                                "{0} user agent override [device: {1}, sku: {2}, user-agent: {3}].  Proceeding",
                                logNamespace, request.DeviceId,
                                request.Sku, request.UserAgent);

                            return new GetPriceResponse(CreateDefaultPriceOptimization(request));
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "{0} failed to get user agent overrides [device: {1}, sku: {2}].  Proceeding",
                            logNamespace, request.DeviceId,
                            request.Sku);
                    }
                }

                var tokenResponse = await GetTokenAsync(logNamespace, e => new GetPriceResponse(e),
                    cancellationToken);
                if (!tokenResponse.IsSuccess) return tokenResponse.ErrorResponse;

                var token = tokenResponse.Token!;

                var httpClient = GetHttpClient(token);
                var query =
                    $"{PriceOptimizationsEndpoint}?deviceId={request.DeviceId}&sku={request.Sku}&defaultPrice={request.DefaultPrice}&overrideToDefaultPrice={request.OverrideToDefaultPrice}";

                _logger.LogDebug("{0} querying {1}", logNamespace, query);

                if (!string.IsNullOrEmpty(request.UserId)) query += $"&userId={request.UserId}";
                if (!string.IsNullOrEmpty(_additionalParameters)) query += $"&{_additionalParameters}";
                return await ExecuteGetApiRequestAsync(httpClient, query, async json =>
                {
                    var priceOptimization = CreatePriceOptimization(json);
                    return new GetPriceResponse(priceOptimization);
                }, e => new GetPriceResponse(e), cancellationToken);
            });

            if (executionResult.IsSuccess)
            {
                _logger.LogDebug("{0} found result [device: {1}, sku: {2}]", logNamespace,
                    executionResult.PriceOptimization!.DeviceId,
                    executionResult.PriceOptimization!.Sku);
                return executionResult!;
            }

            // create a price optimization upon failure using the default price
            var defaultPriceOptimization = CreateDefaultPriceOptimization(request);
            _logger.LogDebug("{0} failed getting price optimization. using fallback [device: {1}, sku: {2}]",
                logNamespace, defaultPriceOptimization.DeviceId,
                defaultPriceOptimization.Sku);
            return new GetPriceResponse(executionResult.Error, defaultPriceOptimization);
        }

        /// <inheritdoc cref="IPriceOptimizationHandler.GetPricesAsync" />
        public async Task<GetPricesResponse> GetPricesAsync(
            GetPricesRequest request,
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@GetPricesAsync@@";

            var executionResult = await _getPriceOptimizationsBatchPolicy.ExecuteAsync(async () =>
            {
                var poRequests = request.Requests.ToList();
                var requestCount = poRequests.Count;

                if (requestCount > MaxRequestSize)
                    throw new ArgumentException($"Max batch size is {MaxRequestSize} requests");

                if (!string.IsNullOrEmpty(request.UserAgent))
                {
                    var userAgentOverridesResponse =
                        await GetPriceOptimizationsUserAgentOverridesAsync(cancellationToken);
                    
                    if (userAgentOverridesResponse.IsSuccess)
                    {
                        if (userAgentOverridesResponse.UserAgentRegexes.Any(regex => regex.IsMatch(request.UserAgent)))
                        {
                            _logger.LogDebug("{0} user agent override [user-agent: {1}].  Proceeding", logNamespace,
                                request.UserAgent);
                            var t = request.Requests.Select(CreateDefaultPriceOptimization);
                            return new GetPricesResponse(request.Requests.Select(CreateDefaultPriceOptimization));

                        }
                    }
                    else
                    {
                        _logger.LogWarning("{0} failed to get user agent overrides", logNamespace);
                    }
                }

                var tokenResponse = await GetTokenAsync(logNamespace,
                    e => new GetPricesResponse(e), cancellationToken);

                if (!tokenResponse.IsSuccess) return tokenResponse.ErrorResponse;

                var token = tokenResponse.Token!;
                var httpClient = GetHttpClient(token);

                var requestUri =
                    PriceOptimizationsEndpoint;
                if (!string.IsNullOrEmpty(_additionalParameters)) requestUri += $"?{_additionalParameters}";

                var batchApiRequest = new
                {
                    requests = poRequests
                };
                var requestJson = JsonConvert.SerializeObject(batchApiRequest, _jsonSerializerSettings);

                return await ExecutePostApiRequestAsync(httpClient, requestUri, requestJson, async responseJson =>
                {
                    var responses = CreatePriceOptimizationArray(responseJson);

                    return new GetPricesResponse(responses);
                }, e => new GetPricesResponse(e), cancellationToken);
            });

            if (executionResult.IsSuccess)
            {
                _logger.LogDebug("{0} found results", logNamespace);
                return executionResult!;
            }

            _logger.LogDebug("{0} failed getting batch price optimizations.  using fallback", logNamespace);
            // todo: fallback price not cached, but note error may be because issue with cache.
            return new GetPricesResponse(executionResult.Error,
                request.Requests.Select(CreateDefaultPriceOptimization));
        }

        public async Task<GetPriceOptimizationsUserAgentOverridesResponse> GetPriceOptimizationsUserAgentOverridesAsync(
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@PriceOptimizationsHandler.GetPriceOptimizationsUserAgentOverridesAsync@@";
            
            var tokenResponse = await GetTokenAsync(logNamespace,
                e => new GetPriceOptimizationsUserAgentOverridesResponse(e), cancellationToken);
            if (!tokenResponse.IsSuccess) return tokenResponse.ErrorResponse;

            var token = tokenResponse.Token!;
            var httpClient = GetHttpClient(token);

            var query = "/pim/v1/priceOptimizationOrgConfig";
            return await ExecuteGetApiRequestAsync(httpClient, query, jsonResponse =>
            {
                var apiResponse = CreateUserAgentRegexes(jsonResponse).Data.UserAgentBlacklist
                    .Where(r => r.Status == UserAgentStatus.Active);
                var compiledRegexes = apiResponse
                    .Select(r => new Regex(r.Regexp, RegexOptions.Singleline | RegexOptions.Compiled)).ToArray();

                var getPriceOptimizationsUserAgentOverridesResponse =
                    new GetPriceOptimizationsUserAgentOverridesResponse(compiledRegexes);
                return Task.FromResult(getPriceOptimizationsUserAgentOverridesResponse);
            }, e => new GetPriceOptimizationsUserAgentOverridesResponse(e), cancellationToken);
        }

        private IAsyncPolicy<GetPricesResponse> CreatePriceOptimizationsBatchResiliencyPolicy(
            PriceOptimizationsHandlerOptions options)
        {
            return CreateResiliencyPolicy(options,
                fallbackOptions: new FallbackOptions<GetPricesResponse>(
                    fallbackPredicate: r => !r.IsSuccess,
                    fallbackAction: (response, ctx, ct) =>
                    {
                        var error = response?.Result?.Error;
                        if (response!.Exception is TimeoutRejectedException) error = PriceOptimizationError.Timeout;

                        _logger.LogError(
                            "@@{0}@@ Price Optimization request failed.  Error {1}.  Exception (if applicable): {2}",
                            nameof(GetPricesAsync), error, response?.Exception?.Message);

                        if (response!.Exception != null)
                        {
                            if (options.ThrowOnFailure) throw response.Exception;

                            return Task.FromResult(
                                new GetPricesResponse(error ?? PriceOptimizationError.Unknown));
                        }

                        if (options.ThrowOnFailure) throw new Exception($"Request failed.  Error {error}");
                        return Task.FromResult(response.Result!);
                    },
                    onFallback: (result, context) => Task.CompletedTask
                ), caller: nameof(GetPricesAsync));
        }

        private IAsyncPolicy<GetPriceResponse> CreatePriceOptimizationResiliencyPolicy(
            PriceOptimizationsHandlerOptions options)
        {
            return CreateResiliencyPolicy(options,
                fallbackOptions: new FallbackOptions<GetPriceResponse>(
                    fallbackPredicate: r => !r.IsSuccess,
                    fallbackAction: (response, ctx, ct) =>
                    {
                        var error = response?.Result?.Error;
                        if (response?.Exception is TimeoutRejectedException) error = PriceOptimizationError.Timeout;

                        _logger.LogError("@@{0}@@ Token request failed.  Error {1}.  Exception (if applicable): {2}",
                            nameof(GetPriceAsync), error, response?.Exception?.Message);

                        if (response!.Exception != null)
                        {
                            if (options.ThrowOnFailure) throw response.Exception;

                            return Task.FromResult(
                                new GetPriceResponse(error ?? PriceOptimizationError.Unknown));
                        }

                        if (options.ThrowOnFailure) throw new Exception($"Request failed.  Error {error}");

                        return Task.FromResult(response.Result!);
                    },
                    onFallback: (result, context) => Task.CompletedTask
                ), caller: nameof(GetPriceAsync));
        }

        private GetUserAgentRegexesApiResponse CreateUserAgentRegexes(string jsonResponse)
        {
            return JsonConvert.DeserializeObject<GetUserAgentRegexesApiResponse>(jsonResponse)!;
        }

        private Task<T> ExecutePostApiRequestAsync<T>(HttpClient httpClient, string requestUri, string requestJson,
            Func<string, Task<T>> onSuccessFunc,
            Func<PriceOptimizationError, T> onFailureFunc, CancellationToken cancellationToken)
        {
            return httpClient.ExecutePostApiRequestAsync(requestUri, requestJson,
                onSuccessFunc: (apiResponseJson, httpStatus) => onSuccessFunc(apiResponseJson),
                onAuthErrorFailure: statusCode => onFailureFunc(PriceOptimizationError.AuthError),
                onBadRequestFailure: () => onFailureFunc(PriceOptimizationError.BadRequest),
                onTimeoutFailure: exception => onFailureFunc(PriceOptimizationError.Timeout),
                onUnknownFailure: (exception, code) => onFailureFunc(PriceOptimizationError.Unknown),
                cancellationToken);
        }

        private Task<T> ExecuteGetApiRequestAsync<T>(HttpClient httpClient, string requestUri,
            Func<string, Task<T>> onSuccessFunc,
            Func<PriceOptimizationError, T> onFailureFunc, CancellationToken cancellationToken)
        {
            return httpClient.ExecuteGetApiRequestAsync(requestUri,
                onSuccessFunc: (apiResponseJson, httpStatus) => onSuccessFunc(apiResponseJson),
                onAuthErrorFailure: statusCode => onFailureFunc(PriceOptimizationError.AuthError),
                onBadRequestFailure: () => onFailureFunc(PriceOptimizationError.BadRequest),
                onTimeoutFailure: exception => onFailureFunc(PriceOptimizationError.Timeout),
                onUnknownFailure: (exception, code) => onFailureFunc(PriceOptimizationError.Unknown),
                cancellationToken);
        }


        private HttpClient GetHttpClient(string token)
        {
            var httpClient = _httpClientFactory.GetClient();
            httpClient.BaseAddress = new Uri(_baseUrl);
            httpClient.Timeout = _httpTimeout;
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            return httpClient;
        }

        private async Task<(bool IsSuccess, string? Token, T ErrorResponse)> GetTokenAsync<T>(string logNamespace,
            Func<PriceOptimizationError, T> failureResponseFunc, CancellationToken cancellationToken)
        {
            var tokenResponse = await _authTokenHandler.GetTokenAsync(cancellationToken);

            if (!tokenResponse.IsSuccess)
            {
                _logger.LogError("{0} failed to get token", logNamespace);

                return (false, null, failureResponseFunc(PriceOptimizationError.AuthError));
            }

            return (true, tokenResponse.Token!, default)!;
        }

        private PriceOptimization CreateDefaultPriceOptimization(GetPriceRequest request)
        {
            return new PriceOptimization(request.Sku, request.DeviceId, request.UserId, request.DefaultPrice, false);
        }

        private IAsyncPolicy<T> CreateResiliencyPolicy<T>(PriceOptimizationsHandlerOptions options,
            FallbackOptions<T> fallbackOptions,
            [CallerMemberName] string caller = default!) where T : IPriceOptimizationResult
        {
            var retryErrors = new[] { PriceOptimizationError.Timeout, PriceOptimizationError.Unknown };
            return ResiliencyPolicyBuilder.BuildPolicy(
                new RetryOptions<T>(r => !r.IsSuccess && retryErrors.Contains(r.Error), 0),
                new TimeoutOptions(options.Timeout),
                new CircuitBreakerOptions<T>(
                    r => !r.IsSuccess && retryErrors.Contains(r.Error),
                    options.NumberOfFailuresBeforeTrippingCircuitBreaker,
                    options.CircuitBreakerBreakDuration,
                    (response, timespan, context) =>
                    {
                        _logger.LogWarning(
                            "@@{0}.{1}@@ Circuit breaker tripped for {2}ms due to error {3}.  Exception details (if applicable): {4}: ",
                            nameof(PriceOptimizationsHandler), caller, timespan.TotalMilliseconds,
                            response.Result.Error, response.Exception?.Message);
                    },
                    context =>
                    {
                        _logger.LogInformation("@@{0}.{1}@@ Circuit breaker reset", nameof(PriceOptimizationsHandler),
                            caller);
                    }),
                fallbackOptions
            );
        }

        private PriceOptimization CreatePriceOptimization(string priceOptimizationJson)
        {
            var apiResponse = JsonConvert.DeserializeObject<PriceOptimization>(priceOptimizationJson)!;
            return apiResponse;
        }

        private PriceOptimization[] CreatePriceOptimizationArray(string priceOptimizationJson)
        {
            var apiResponse =
                JsonConvert.DeserializeObject<PriceOptimization[]>(priceOptimizationJson)!;
            return apiResponse;
        }

        private class UserAgentRegex
        {
            public UserAgentRegex(string name, string regexp, UserAgentStatus status)
            {
                Name = name;
                Regexp = regexp;
                Status = status;
            }

            public string Name { get; }
            public string Regexp { get; }
            public UserAgentStatus Status { get; }
        }

        private class GetUserAgentRegexesApiResponse
        {
            public GetUserAgentRegexesApiResponse(GetUserAgentRegexesApiResponseData data)
            {
                Data = data;
            }

            public GetUserAgentRegexesApiResponseData Data { get; }

            public class GetUserAgentRegexesApiResponseData
            {
                public GetUserAgentRegexesApiResponseData(UserAgentRegex[] userAgentBlacklist)
                {
                    UserAgentBlacklist = userAgentBlacklist;
                }

                public UserAgentRegex[] UserAgentBlacklist { get; }
            }
        }
    }
}