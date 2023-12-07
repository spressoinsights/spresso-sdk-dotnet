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
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using Polly.Timeout;
using SpressoAI.Sdk.Core.Auth;
using SpressoAI.Sdk.Core.Connectivity;
using SpressoAI.Sdk.Core.Resiliency;

namespace SpressoAI.Sdk.Pricing
{
    public class SpressoHandler : ISpressoHandler
    {
        public enum UserAgentStatus : byte
        {
            Active = 0,
            Disabled = 1,
            Deleted = 2
        }

        private const string PriceOptimizationsEndpoint = "/pim/v1/prices";
        private const string CatalogUpdatesEndpoint = "/pim/v1/variants";
        private const string PriceVerificationEndpoint = "/pim/v1/prices/verify";
        private const string OptimizedSkusKey = "Spresso.Core.OptimizedSkusKey";
        private const int MaxRequestSize = 500;
        private readonly string _additionalParameters;
        private readonly IAuthTokenHandler _authTokenHandler;
        private readonly string _baseUrl;
        private readonly IAsyncPolicy<GetPriceResponse> _getPriceOptimizationPolicy;
        private readonly IAsyncPolicy<GetPricesResponse> _getPriceOptimizationBatchPolicy;
        private readonly SpressoHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _httpTimeout;
        private readonly IDistributedCache _cache;

        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        private readonly ILogger<ISpressoHandler> _logger;

         /// <summary>
        ///     Initializes a new instance of the <see cref="SpressoHandler" /> class.
        /// </summary>
        /// <param name="authTokenHandler">The instance of AuthTokenHandler</param>
        /// <param name="options">Price optimization handler configuration</param>
        public SpressoHandler(IAuthTokenHandler authTokenHandler,
            SpressoHandlerOptions? options = null)
        {
            options ??= new SpressoHandlerOptions();
            _logger = options.Logger;
            _authTokenHandler = authTokenHandler;
            _httpClientFactory = options.SpressoHttpClientFactory;
            _baseUrl = options.SpressoBaseUrl;
            _httpTimeout = options.HttpTimeout;
            _additionalParameters = options.AdditionalParameters;
            _getPriceOptimizationPolicy = CreateSpressoResiliencyPolicy(options);
            _getPriceOptimizationBatchPolicy = CreateSpressoBatchResiliencyPolicy(options);
            _cache = new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

            // Premptively cache optimized skus, don't await
            _ = CacheOptimizedSkusAsync();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SpressoHandler" /> class.
        /// </summary>
        /// <param name="clientId">The client key provided for your application(s)</param>
        /// <param name="clientSecret">The client secret provided for your application(s)</param>
        /// <param name="options">Price optimization handler configuration</param>
        public SpressoHandler(string clientId, string clientSecret,
            SpressoHandlerOptions? options = null)
        {
            options ??= new SpressoHandlerOptions();
            var authOptions = new AuthTokenHandlerOptions
            {
                SpressoBaseAuthUrl = options.SpressoBaseUrl // in case this is overridden in options, we use the same overridde for auth
            };
            _logger = options.Logger;
            _authTokenHandler = new AuthTokenHandler(clientId, clientSecret, authOptions);
            _httpClientFactory = options.SpressoHttpClientFactory;
            _baseUrl = options.SpressoBaseUrl;
            _httpTimeout = options.HttpTimeout;
            _additionalParameters = options.AdditionalParameters;
            _getPriceOptimizationPolicy = CreateSpressoResiliencyPolicy(options);
            _getPriceOptimizationBatchPolicy = CreateSpressoBatchResiliencyPolicy(options);
            _cache = new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

            // Premptively cache optimized skus, don't await
            _ = CacheOptimizedSkusAsync();
        }

        /// <inheritdoc cref="ISpressoHandler.GetPriceAsync" />
        public async Task<GetPriceResponse> GetPriceAsync(
            GetPriceRequest request,
            CancellationToken cancellationToken = default
        ) {
            return await GetPriceAsync(request, null, null, cancellationToken);
        }

        public async Task<GetPriceResponse> GetPriceAsync(
            GetPriceRequest request,
            string? originalIP,
            Dictionary<string, string>? httpHeaders,
            CancellationToken cancellationToken = default
        ) {
            const string logNamespace = "@@GetPriceAsync@@";
            
            var executionResult = await _getPriceOptimizationPolicy.ExecuteAsync(async () =>
            {
                var requestArray = new List<GetPriceRequest>
                {
                    request
                };

                if (SkipInactiveSkus(requestArray))
                {
                    return new GetPriceResponse(CreateDefaultPriceOptimization(request));
                }

                if (!string.IsNullOrEmpty(request.UserAgent))
                {
                    var userAgentOverridesResponse =
                        await GetPriceOptimizationsUserAgentOverridesAsync(cancellationToken);
                    if (userAgentOverridesResponse.IsSuccess)
                    {
                        if (userAgentOverridesResponse.UserAgentRegexes.Any(regex => regex.IsMatch(request.UserAgent)))
                        {
                            _logger.LogDebug(
                                "{0} user agent override [device: {1}, itemId: {2}, user-agent: {3}].  Proceeding",
                                logNamespace, request.DeviceId,
                                request.ItemId, request.UserAgent);

                            return new GetPriceResponse(CreateDefaultPriceOptimization(request));
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "{0} failed to get user agent overrides [device: {1}, itemId: {2}].  Proceeding",
                            logNamespace, request.DeviceId,
                            request.ItemId);
                    }
                }

                var tokenResponse = await GetTokenAsync(logNamespace, e => new GetPriceResponse(e),
                    cancellationToken);
                if (!tokenResponse.IsSuccess) return tokenResponse.ErrorResponse;

                var token = tokenResponse.Token!;

                var httpClient = GetHttpClient(token);
                var query =
                    $"{PriceOptimizationsEndpoint}?deviceId={request.DeviceId}&itemId={request.ItemId}&defaultPrice={request.DefaultPrice}&overrideToDefaultPrice={request.OverrideToDefaultPrice}";

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
                _logger.LogDebug("{0} found result [device: {1}, itemId: {2}]", logNamespace,
                    executionResult.PriceOptimization!.DeviceId,
                    executionResult.PriceOptimization!.ItemId);
                return executionResult!;
            }

            // create a price optimization upon failure using the default price
            var defaultPriceOptimization = CreateDefaultPriceOptimization(request);
            _logger.LogDebug("{0} failed getting price optimization. using fallback [device: {1}, itemId: {2}]",
                logNamespace, defaultPriceOptimization.DeviceId,
                defaultPriceOptimization.ItemId);
            return new GetPriceResponse(executionResult.Error, defaultPriceOptimization);
        }

        /// <inheritdoc cref="ISpressoHandler.GetPricesAsync" />
        public async Task<GetPricesResponse> GetPricesAsync(
            GetPricesRequest request,
            CancellationToken cancellationToken = default
        ) {
            return await GetPricesAsync(request, null, null, cancellationToken);
        }

        public async Task<GetPricesResponse> GetPricesAsync(
            GetPricesRequest request,
            string? originalIP,
            Dictionary<string, string>? httpHeaders,
            CancellationToken cancellationToken = default
        ) {
            const string logNamespace = "@@GetPricesAsync@@";
            var executionResult = await _getPriceOptimizationBatchPolicy.ExecuteAsync(async () =>
            {
                var poRequests = request.Requests.ToList();
                var requestCount = poRequests.Count;

                if (requestCount > MaxRequestSize)
                    throw new ArgumentException($"Max batch size is {MaxRequestSize} requests");

                if (SkipInactiveSkus(poRequests))
                {
                    return new GetPricesResponse(request.Requests.Select(CreateDefaultPriceOptimization));
                }

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

        private bool SkipInactiveSkus(List<GetPriceRequest> requests)
        {
            // Fetch optimized skus, don't await!
            _ = CacheOptimizedSkusAsync();

            var optimizedSkus = GetCachedOptimizedSkuList();
            if (optimizedSkus == null)
            {
                return false;
            }

            /* We only skip if ALL skus are non optimized
             * Why? The point of this is to minimize the API roundtrip.
             * If even one sku IS optimized we won't be able to skip, so no point adding the extra complexity of partial skips
             */
            var someOptimized = requests.Any(request => optimizedSkus.Skus.Contains(request.ItemId));
            if (optimizedSkus.SkipInactive && !someOptimized)
            {
                _logger.LogDebug("All SKUs are non-optimized, short-circuiting...");
                return true;
            }

            _logger.LogDebug("Found optimized SKU, making API request...");
            return false;
        }

        public async Task<GetPriceOptimizationsUserAgentOverridesResponse> GetPriceOptimizationsUserAgentOverridesAsync(
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@SpressoHandler.GetPriceOptimizationsUserAgentOverridesAsync@@";
            const string cacheKey = "Spresso.Core.UserAgentKey";

            var cachedUserAgents = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedUserAgents != null)
            {
                _logger.LogDebug("{0} cache hit", cacheKey);
                return ProcessUserAgentResponse(cachedUserAgents);
            }
            
            var tokenResponse = await GetTokenAsync(logNamespace,
                e => new GetPriceOptimizationsUserAgentOverridesResponse(e), cancellationToken);
            if (!tokenResponse.IsSuccess) return tokenResponse.ErrorResponse;

            var token = tokenResponse.Token!;
            var httpClient = GetHttpClient(token);

            var query = "/pim/v1/priceOptimizationOrgConfig";
            return await ExecuteGetApiRequestAsync(httpClient, query, jsonResponse =>
            {
                _logger.LogDebug("{0} cache miss", cacheKey);
                _cache.SetStringAsync(cacheKey, jsonResponse, 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(1)
                    }, cancellationToken);
                return Task.FromResult(ProcessUserAgentResponse(jsonResponse));
            }, e => new GetPriceOptimizationsUserAgentOverridesResponse(e), cancellationToken);
        }

        private async Task<GetOptimizedSkusResponse> CacheOptimizedSkusAsync(
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@SpressoHandler.GetOptimizedSkusAsync@@";

            var cachedSkus = GetCachedOptimizedSkuList();
            if (cachedSkus != null)
            {
                _logger.LogDebug("{0} cache hit", OptimizedSkusKey);
                return cachedSkus;
            }

            var tokenResponse = await GetTokenAsync(logNamespace,
                e => new GetOptimizedSkusResponse(0, new HashSet<string>(), false), cancellationToken);
            if (!tokenResponse.IsSuccess) return tokenResponse.ErrorResponse;

            var token = tokenResponse.Token!;
            var httpClient = GetHttpClient(token);

            var query = "/pim/v1/variants/optimizedSKUs";
            return await ExecuteGetApiRequestAsync(httpClient, query, jsonResponse =>
            {
                var result = ProcessOptimizedSkuResponse(jsonResponse);
                _logger.LogDebug("{0} cache miss", OptimizedSkusKey);
                _cache.SetStringAsync(OptimizedSkusKey, jsonResponse, 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = DateTimeOffset.FromUnixTimeSeconds(result.ExpiresAt)
                    }, cancellationToken);
                return Task.FromResult(result);
            }, e => new GetOptimizedSkusResponse(0, new HashSet<string>(), false), cancellationToken);
        }

        private GetOptimizedSkusResponse? GetCachedOptimizedSkuList() {
            var cachedSkus = _cache.GetString(OptimizedSkusKey);
            if (cachedSkus != null)
            {
                _logger.LogDebug("{0} cache hit", OptimizedSkusKey);
                return ProcessOptimizedSkuResponse(cachedSkus);
            }

            return null;
        }

        /// <inheritdoc cref="ISpressoHandler.UpdateCatalogAsync" />
        public async Task<CatalogUpdateResponse> UpdateCatalogAsync(
            CatalogUpdatesRequest request,
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@UpdateCatalogAsync@@";

            var updateRequests = request.Requests.ToList();
            var requestCount = updateRequests.Count;

            if (requestCount > MaxRequestSize)
                throw new ArgumentException($"Max batch size is {MaxRequestSize} requests");

            var tokenResponse = await GetTokenAsync(logNamespace, e => new CatalogUpdateResponse(e), cancellationToken);

            if (!tokenResponse.IsSuccess) return tokenResponse.ErrorResponse;

            var token = tokenResponse.Token!;
            var httpClient = GetHttpClient(token);

            var batchApiRequest = new
            {
                requests = updateRequests
            };
            var requestJson = JsonConvert.SerializeObject(batchApiRequest, _jsonSerializerSettings);

            return await ExecutePutApiRequestAsync(httpClient, CatalogUpdatesEndpoint, requestJson, async responseJson =>
            {
                return new CatalogUpdateResponse();
            }, e => new CatalogUpdateResponse(e), cancellationToken);
        }

        /// <inheritdoc cref="ISpressoHandler.VerifyPricesAsync" />
        public async Task<PriceVerificationResponse> VerifyPricesAsync(
            PriceVerificationsRequest request,
            CancellationToken cancellationToken = default)
        {
            const string logNamespace = "@@VerifyPricesAsync@@";

            var verificationRequests = request.Requests.ToList();
            var requestCount = verificationRequests.Count;

            if (requestCount > MaxRequestSize)
                throw new ArgumentException($"Max batch size is {MaxRequestSize} requests");

            var tokenResponse = await GetTokenAsync(logNamespace, e => new PriceVerificationResponse(e), cancellationToken);

            if (!tokenResponse.IsSuccess) return tokenResponse.ErrorResponse;

            var token = tokenResponse.Token!;
            var httpClient = GetHttpClient(token);

            var batchApiRequest = new
            {
                requests = verificationRequests
            };
            var requestJson = JsonConvert.SerializeObject(batchApiRequest, _jsonSerializerSettings);

            return await ExecutePostApiRequestAsync(httpClient, PriceVerificationEndpoint, requestJson, async responseJson =>
            {
                var responses = CreatePriceVerificationArray(responseJson);

                return new PriceVerificationResponse(responses);
            }, e => new PriceVerificationResponse(e), cancellationToken);
        }

        private IAsyncPolicy<GetPricesResponse> CreateSpressoBatchResiliencyPolicy(
            SpressoHandlerOptions options)
        {
            return CreateResiliencyPolicy(options,
                fallbackOptions: new FallbackOptions<GetPricesResponse>(
                    fallbackPredicate: r => !r.IsSuccess,
                    fallbackAction: (response, ctx, ct) =>
                    {
                        var error = response?.Result?.Error;
                        if (response!.Exception is TimeoutRejectedException) error = SpressoError.Timeout;

                        _logger.LogError(
                            "@@{0}@@ Price Optimization request failed.  Error {1}.  Exception (if applicable): {2}",
                            nameof(GetPricesAsync), error, response?.Exception?.Message);

                        if (response!.Exception != null)
                        {
                            if (options.ThrowOnFailure) throw response.Exception;

                            return Task.FromResult(
                                new GetPricesResponse(error ?? SpressoError.Unknown));
                        }

                        if (options.ThrowOnFailure) throw new Exception($"Request failed.  Error {error}");
                        return Task.FromResult(response.Result!);
                    },
                    onFallback: (result, context) => Task.CompletedTask
                ), caller: nameof(GetPricesAsync));
        }

        private IAsyncPolicy<GetPriceResponse> CreateSpressoResiliencyPolicy(
            SpressoHandlerOptions options)
        {
            return CreateResiliencyPolicy(options,
                fallbackOptions: new FallbackOptions<GetPriceResponse>(
                    fallbackPredicate: r => !r.IsSuccess,
                    fallbackAction: (response, ctx, ct) =>
                    {
                        var error = response?.Result?.Error;
                        if (response?.Exception is TimeoutRejectedException) error = SpressoError.Timeout;

                        _logger.LogError("@@{0}@@ Token request failed.  Error {1}.  Exception (if applicable): {2}",
                            nameof(GetPriceAsync), error, response?.Exception?.Message);

                        if (response!.Exception != null)
                        {
                            if (options.ThrowOnFailure) throw response.Exception;

                            return Task.FromResult(
                                new GetPriceResponse(error ?? SpressoError.Unknown));
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

        private GetPriceOptimizationsUserAgentOverridesResponse ProcessUserAgentResponse(string json)
        {
            var parsedResponse = CreateUserAgentRegexes(json).Data.UserAgentBlacklist.Where(r => r.Status == UserAgentStatus.Active);
            var compiledRegexes = parsedResponse
                .Select(r => new Regex(r.Regexp, RegexOptions.Singleline | RegexOptions.Compiled)).ToArray();

            var getPriceOptimizationsUserAgentOverridesResponse =
                new GetPriceOptimizationsUserAgentOverridesResponse(compiledRegexes);
            
            return getPriceOptimizationsUserAgentOverridesResponse;
        }

        private GetOptimizedSkusResponse ProcessOptimizedSkuResponse(string jsonResponse)
        {
            return JsonConvert.DeserializeObject<GetOptimizedSkusResponse>(jsonResponse)!;
        }

        private Task<T> ExecutePostApiRequestAsync<T>(HttpClient httpClient, string requestUri, string requestJson,
            Func<string, Task<T>> onSuccessFunc,
            Func<SpressoError, T> onFailureFunc, CancellationToken cancellationToken,
            string? originalIP = null, Dictionary<string, string>? httpHeaders = null
            )
        {
            return httpClient.ExecutePostApiRequestAsync(requestUri, requestJson,
                onSuccessFunc: (apiResponseJson, httpStatus) => onSuccessFunc(apiResponseJson),
                onAuthErrorFailure: statusCode => onFailureFunc(SpressoError.AuthError),
                onBadRequestFailure: () => onFailureFunc(SpressoError.BadRequest),
                onTimeoutFailure: exception => onFailureFunc(SpressoError.Timeout),
                onUnknownFailure: (exception, code) => onFailureFunc(SpressoError.Unknown),
                cancellationToken, originalIP, httpHeaders);
        }

        private Task<T> ExecuteGetApiRequestAsync<T>(HttpClient httpClient, string requestUri,
            Func<string, Task<T>> onSuccessFunc,
            Func<SpressoError, T> onFailureFunc, CancellationToken cancellationToken,
            string? originalIP = null, Dictionary<string, string>? httpHeaders = null
            )
        {
            return httpClient.ExecuteGetApiRequestAsync(requestUri,
                onSuccessFunc: (apiResponseJson, httpStatus) => onSuccessFunc(apiResponseJson),
                onAuthErrorFailure: statusCode => onFailureFunc(SpressoError.AuthError),
                onBadRequestFailure: () => onFailureFunc(SpressoError.BadRequest),
                onTimeoutFailure: exception => onFailureFunc(SpressoError.Timeout),
                onUnknownFailure: (exception, code) => onFailureFunc(SpressoError.Unknown),
                cancellationToken, originalIP, httpHeaders);
        }

        private Task<T> ExecutePutApiRequestAsync<T>(HttpClient httpClient, string requestUri, string requestJson,
            Func<string, Task<T>> onSuccessFunc,
            Func<SpressoError, T> onFailureFunc, CancellationToken cancellationToken)
        {
            return httpClient.ExecutePutApiRequestAsync(requestUri, requestJson,
                onSuccessFunc: (apiResponseJson, httpStatus) => onSuccessFunc(apiResponseJson),
                onAuthErrorFailure: statusCode => onFailureFunc(SpressoError.AuthError),
                onBadRequestFailure: () => onFailureFunc(SpressoError.BadRequest),
                onTimeoutFailure: exception => onFailureFunc(SpressoError.Timeout),
                onUnknownFailure: (exception, code) => onFailureFunc(SpressoError.Unknown),
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
            Func<SpressoError, T> failureResponseFunc, CancellationToken cancellationToken)
        {
            var tokenResponse = await _authTokenHandler.GetTokenAsync(cancellationToken);

            if (!tokenResponse.IsSuccess)
            {
                _logger.LogError("{0} failed to get token", logNamespace);

                return (false, null, failureResponseFunc(SpressoError.AuthError));
            }

            return (true, tokenResponse.Token!, default)!;
        }

        private PriceOptimization CreateDefaultPriceOptimization(GetPriceRequest request)
        {
            return new PriceOptimization(request.ItemId, request.DeviceId, request.UserId, request.DefaultPrice, false);
        }

        private IAsyncPolicy<T> CreateResiliencyPolicy<T>(SpressoHandlerOptions options,
            FallbackOptions<T> fallbackOptions,
            [CallerMemberName] string caller = default!) where T : IPriceOptimizationResult
        {
            var retryErrors = new[] { SpressoError.Timeout, SpressoError.Unknown };
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
                            nameof(SpressoHandler), caller, timespan.TotalMilliseconds,
                            response.Result.Error, response.Exception?.Message);
                    },
                    context =>
                    {
                        _logger.LogInformation("@@{0}.{1}@@ Circuit breaker reset", nameof(SpressoHandler),
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

        private PriceVerification[] CreatePriceVerificationArray(string priceVerificationJson)
        {
            var apiResponse =
                JsonConvert.DeserializeObject<PriceVerification[]>(priceVerificationJson)!;
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