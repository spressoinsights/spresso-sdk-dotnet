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
        Task<GetPriceOptimizationResponse> GetPriceOptimizationAsync(GetPriceOptimizationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets multiple price optimizations
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<GetBatchPriceOptimizationsResponse> GetBatchPriceOptimizationsAsync(GetBatchPriceOptimizationsRequest request,
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

    public sealed class GetPriceOptimizationResponse : IPriceOptimizationResult
    {
        public bool IsSuccess => Error == PriceOptimizationError.None;
        public PriceOptimization? PriceOptimization { get; }
        public PriceOptimizationError Error { get; }

        public GetPriceOptimizationResponse(PriceOptimization priceOptimization)
        {
            PriceOptimization = priceOptimization;
            Error = PriceOptimizationError.None;
        }

        public GetPriceOptimizationResponse(PriceOptimizationError error)
        {
            PriceOptimization = null;
            Error = error;
        }

        public GetPriceOptimizationResponse(PriceOptimizationError error, PriceOptimization priceOptimization)
        {
            PriceOptimization = priceOptimization;
            Error = error;
        }
    }

    public sealed class GetBatchPriceOptimizationsResponse : IPriceOptimizationResult
    {
        public bool IsSuccess => Error == PriceOptimizationError.None;
        public PriceOptimizationError Error { get; }
        public IEnumerable<PriceOptimization> PriceOptimizations { get; }

        public GetBatchPriceOptimizationsResponse(IEnumerable<PriceOptimization> priceOptimizations)
        {
            PriceOptimizations = priceOptimizations;
            Error = PriceOptimizationError.None;
        }

        public GetBatchPriceOptimizationsResponse(PriceOptimizationError error)
        {
            PriceOptimizations = Array.Empty<PriceOptimization>();
            Error = error;
        }

        public GetBatchPriceOptimizationsResponse(PriceOptimizationError error, IEnumerable<PriceOptimization> priceOptimizations)
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

    public class GetPriceOptimizationRequest
    {
        public string DeviceId { get; }
        public string ItemId { get; }
        public string? UserId { get; }
        public decimal DefaultPrice { get; }
        public bool OverrideToDefaultPrice { get; }
        public string? UserAgent { get; }

        public GetPriceOptimizationRequest(string deviceId, string itemId, decimal defaultPrice, string? userId = null, bool overrideToDefaultPrice = false,
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


    public class GetBatchPriceOptimizationsRequest
    {
        public IEnumerable<GetPriceOptimizationRequest> Requests { get; }
        public string? UserAgent { get; }

        public GetBatchPriceOptimizationsRequest(IEnumerable<GetPriceOptimizationRequest> requests, string? userAgent = default)
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