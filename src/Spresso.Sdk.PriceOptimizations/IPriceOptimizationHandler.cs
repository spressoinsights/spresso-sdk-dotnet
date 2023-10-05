using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Spresso.Sdk.PriceOptimizations
{
    public interface IPriceOptimizationHandler
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
    }

    public sealed class GetPriceOptimizationsUserAgentOverridesResponse : IPriceOptimizationResult
    {
        public Regex[] UserAgentRegexes { get; }
        public bool IsSuccess => Error == PriceOptimizationError.None;
        public PriceOptimizationError Error { get; }

        public GetPriceOptimizationsUserAgentOverridesResponse(Regex[]? userAgentRegexes)
        {
            UserAgentRegexes = userAgentRegexes ?? Array.Empty<Regex>();
            Error = PriceOptimizationError.None;
        }

        public GetPriceOptimizationsUserAgentOverridesResponse(PriceOptimizationError error)
        {
            UserAgentRegexes = Array.Empty<Regex>();
            Error = error;
        }
    }


    public interface IPriceOptimizationResult
    {
        public bool IsSuccess { get; }
        public PriceOptimizationError Error { get; }
    }

    public sealed class GetPriceResponse : IPriceOptimizationResult
    {
        public bool IsSuccess => Error == PriceOptimizationError.None;
        public PriceOptimization? PriceOptimization { get; }
        public PriceOptimizationError Error { get; }

        public GetPriceResponse(PriceOptimization priceOptimization)
        {
            PriceOptimization = priceOptimization;
            Error = PriceOptimizationError.None;
        }

        public GetPriceResponse(PriceOptimizationError error)
        {
            PriceOptimization = null;
            Error = error;
        }

        public GetPriceResponse(PriceOptimizationError error, PriceOptimization priceOptimization)
        {
            PriceOptimization = priceOptimization;
            Error = error;
        }
    }

    public sealed class GetPricesResponse : IPriceOptimizationResult
    {
        public bool IsSuccess => Error == PriceOptimizationError.None;
        public PriceOptimizationError Error { get; }
        public IEnumerable<PriceOptimization> PriceOptimizations { get; }

        public GetPricesResponse(IEnumerable<PriceOptimization> priceOptimizations)
        {
            PriceOptimizations = priceOptimizations;
            Error = PriceOptimizationError.None;
        }

        public GetPricesResponse(PriceOptimizationError error)
        {
            PriceOptimizations = Array.Empty<PriceOptimization>();
            Error = error;
        }

        public GetPricesResponse(PriceOptimizationError error, IEnumerable<PriceOptimization> priceOptimizations)
        {
            PriceOptimizations = priceOptimizations;
            Error = error;
        }
    }


    public sealed class PriceOptimization
    {
        public PriceOptimization(string sku, string deviceId, string? userId, decimal price, bool isPriceOptimized)
        {
            Sku = sku;
            DeviceId = deviceId;
            UserId = userId;
            Price = price;
            IsPriceOptimized = isPriceOptimized;
        }

        public string Sku { get; }
        public string DeviceId { get; }
        public string? UserId { get;}
        public decimal Price { get; }
        public bool IsPriceOptimized { get; }
    }

    public class GetPriceRequest
    {
        public string DeviceId { get; }
        public string Sku { get; }
        public string? UserId { get; }
        public decimal DefaultPrice { get; }
        public bool OverrideToDefaultPrice { get; }
        public string? UserAgent { get; }

        public GetPriceRequest(string deviceId, string sku, decimal defaultPrice, string? userId = null, bool overrideToDefaultPrice = false,
            string? userAgent = default)
        {
            DeviceId = deviceId;
            Sku = sku;
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


    public enum PriceOptimizationError: byte
    {
        None,
        AuthError,
        BadRequest,
        Timeout,
        Unknown
    }
}