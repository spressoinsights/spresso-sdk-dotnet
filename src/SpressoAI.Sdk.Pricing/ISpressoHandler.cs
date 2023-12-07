using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SpressoAI.Sdk.Pricing
{

    public enum PriceVerificationStatus : byte
    {
        Invalid = 0,
        SpressoPrice = 1,
        DevicePrice = 2
    }

    public interface ISpressoHandler
    {
        /// <summary>
        ///     Gets a single price optimization
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<GetPriceResponse> GetPriceAsync(GetPriceRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets multiple price optimizations
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<GetPricesResponse> GetPricesAsync(GetPricesRequest request,
            CancellationToken cancellationToken = default);

        Task<GetPriceOptimizationsUserAgentOverridesResponse> GetPriceOptimizationsUserAgentOverridesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Updates Spresso Product Catalog
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CatalogUpdateResponse> UpdateCatalogAsync(CatalogUpdatesRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Verifies Spresso Pricing
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<PriceVerificationResponse> VerifyPricesAsync(PriceVerificationsRequest request, CancellationToken cancellationToken = default);
    }

    public sealed class GetPriceOptimizationsUserAgentOverridesResponse : IPriceOptimizationResult
    {
        public Regex[] UserAgentRegexes { get; }
        public bool IsSuccess => Error == SpressoError.None;
        public SpressoError Error { get; }

        public GetPriceOptimizationsUserAgentOverridesResponse(Regex[]? userAgentRegexes)
        {
            UserAgentRegexes = userAgentRegexes ?? Array.Empty<Regex>();
            Error = SpressoError.None;
        }

        public GetPriceOptimizationsUserAgentOverridesResponse(SpressoError error)
        {
            UserAgentRegexes = Array.Empty<Regex>();
            Error = error;
        }
    }

    public sealed class GetOptimizedSkusResponse
    {
        public HashSet<string> Skus { get; }

        public bool SkipInactive { get; }

        public long ExpiresAt { get; }

        public GetOptimizedSkusResponse()
        {
            Skus = new HashSet<string>();
            SkipInactive = false;
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1; // set expiration to the past
        }

        public GetOptimizedSkusResponse(long expiresAt, HashSet<string> skus, bool skipInactive)
        {
            Skus = skus;
            ExpiresAt = expiresAt;
            SkipInactive = skipInactive;
        }
    }

    public interface IPriceOptimizationResult
    {
        public bool IsSuccess { get; }
        public SpressoError Error { get; }
    }

    public sealed class GetPriceResponse : IPriceOptimizationResult
    {
        public bool IsSuccess => Error == SpressoError.None;
        public PriceOptimization? PriceOptimization { get; }
        public SpressoError Error { get; }

        public GetPriceResponse(PriceOptimization priceOptimization)
        {
            PriceOptimization = priceOptimization;
            Error = SpressoError.None;
        }

        public GetPriceResponse(SpressoError error)
        {
            PriceOptimization = null;
            Error = error;
        }

        public GetPriceResponse(SpressoError error, PriceOptimization priceOptimization)
        {
            PriceOptimization = priceOptimization;
            Error = error;
        }
    }

    public sealed class GetPricesResponse : IPriceOptimizationResult
    {
        public bool IsSuccess => Error == SpressoError.None;
        public SpressoError Error { get; }
        public IEnumerable<PriceOptimization> PriceOptimizations { get; }

        public GetPricesResponse(IEnumerable<PriceOptimization> priceOptimizations)
        {
            PriceOptimizations = priceOptimizations;
            Error = SpressoError.None;
        }

        public GetPricesResponse(SpressoError error)
        {
            PriceOptimizations = Array.Empty<PriceOptimization>();
            Error = error;
        }

        public GetPricesResponse(SpressoError error, IEnumerable<PriceOptimization> priceOptimizations)
        {
            PriceOptimizations = priceOptimizations;
            Error = error;
        }
    }

    public sealed class PriceOptimization
    {
        public PriceOptimization(string itemId, string deviceId, string? userId, decimal price, bool isPriceOptimized)
        {
            ItemId = itemId;
            DeviceId = deviceId;
            UserId = userId;
            Price = price;
            IsPriceOptimized = isPriceOptimized;
        }

        public string ItemId { get; }
        public string DeviceId { get; }
        public string? UserId { get;}
        public decimal Price { get; }
        public bool IsPriceOptimized { get; }
    }

    public class GetPriceRequest
    {
        public string DeviceId { get; }
        public string ItemId { get; }
        public string? UserId { get; }
        public decimal DefaultPrice { get; }
        public bool OverrideToDefaultPrice { get; }
        public string? UserAgent { get; }

        public GetPriceRequest(string deviceId, string itemId, decimal defaultPrice, string? userId = null, bool overrideToDefaultPrice = false,
            string? userAgent = default)
        {
            DeviceId = deviceId;
            ItemId = itemId;
            UserId = userId;
            DefaultPrice = defaultPrice;
            OverrideToDefaultPrice = overrideToDefaultPrice;
            UserAgent = userAgent;
        }
    }

    public class GetPricesRequest
    {
        public IEnumerable<GetPriceRequest> Requests { get; }
        public string? UserAgent { get; }

        public GetPricesRequest(IEnumerable<GetPriceRequest> requests, string? userAgent = default)
        {
            Requests = requests;
            UserAgent = userAgent;
        }
    }

    public enum SpressoError: byte
    {
        None,
        AuthError,
        BadRequest,
        Timeout,
        Unknown
    }

    public class CatalogUpdateRequest
    {
        public string Sku { get; }
        public string Name { get; }
        public decimal Price { get; }
        public decimal Cost { get; }
        public string? ProductId { get; }
        public string? Category { get; }
        public string? Upc { get; }
        public string? Brand { get; }
        public decimal? MapPrice { get; }
        public decimal? MsrpPrice { get; }

        public CatalogUpdateRequest(
            string sku,
            string name,
            decimal cost,
            decimal price,
            string? productId = null,
            string? category = null,
            string? upc = null,
            string? brand = null,
            decimal? mapPrice = null,
            decimal? msrpPrice = null
        ) {
            Sku = sku;
            Name = name;
            Cost = cost;
            Price = price;
            ProductId = productId;
            Category = category;
            Upc = upc;
            Brand = brand;
            MapPrice = mapPrice;
            MsrpPrice = msrpPrice;
        }
    }

    public class CatalogUpdatesRequest
    {
        public IEnumerable<CatalogUpdateRequest> Requests { get; }

        public CatalogUpdatesRequest(IEnumerable<CatalogUpdateRequest> requests)
        {
            Requests = requests;
        }
    }

    public sealed class CatalogUpdateResponse
    {
        public bool IsSuccess => Error == SpressoError.None;
        public SpressoError Error { get; }

        public CatalogUpdateResponse()
        {
            Error = SpressoError.None;
        }

        public CatalogUpdateResponse(SpressoError error)
        {
            Error = error;
        }
    }

    public class PriceVerificationRequest
    {
        public string ItemId { get; }
        public decimal Price { get; }
        public string? DeviceId { get; }

        public PriceVerificationRequest(
            string itemId,
            decimal price,
            string? deviceId = null
        ) {
            ItemId = itemId;
            Price = price;
            DeviceId = deviceId;
        }
    }

    public class PriceVerificationsRequest
    {
        public IEnumerable<PriceVerificationRequest> Requests { get; }

        public PriceVerificationsRequest(IEnumerable<PriceVerificationRequest> requests)
        {
            Requests = requests;
        }
    }

    public sealed class PriceVerification
    {
        public string ItemId { get; }
        public string DeviceId { get; }
        public decimal Price { get; }
        public PriceVerificationStatus PriceStatus { get; }
        public decimal? CurrentValidPrice { get; }

        public PriceVerification(string itemId, string deviceId, decimal price, PriceVerificationStatus priceStatus, decimal? currentValidPrice)
        {
            ItemId = itemId;
            DeviceId = deviceId;
            Price = price;
            PriceStatus = priceStatus;
            CurrentValidPrice = currentValidPrice;
        }
    }

    public sealed class PriceVerificationResponse
    {
        public bool IsSuccess => Error == SpressoError.None;
        public SpressoError Error { get; }
        public IEnumerable<PriceVerification> PriceVerifications { get; }

        public PriceVerificationResponse(IEnumerable<PriceVerification> priceVerifications)
        {
            PriceVerifications = priceVerifications;
            Error = SpressoError.None;
        }

        public PriceVerificationResponse(SpressoError error)
        {
            PriceVerifications = Array.Empty<PriceVerification>();
            Error = error;
        }

        public PriceVerificationResponse(SpressoError error, IEnumerable<PriceVerification> priceVerifications)
        {
            PriceVerifications = priceVerifications;
            Error = error;
        }
    }
}