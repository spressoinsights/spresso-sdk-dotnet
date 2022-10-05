using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Spresso.Sdk.PriceOptimizations
{
    public interface IPriceOptimizationHandler
    {
        Task<GetPriceOptimizationResponse> GetPriceOptimizationAsync(GetPriceOptimizationRequest request, CancellationToken cancellationToken = default);

        Task<GetBatchPriceOptimizationsResponse> GetBatchPriceOptimizationsAsync(GetBatchPriceOptimizationsRequest request,
            CancellationToken cancellationToken = default);

        Task<GetPriceOptimizationsUserAgentOverridesResponse> GetPriceOptimizationsUserAgentOverridesAsync(CancellationToken cancellationToken = default);
    }

    public readonly struct GetPriceOptimizationsUserAgentOverridesResponse : IPriceOptimizationResult
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

    public readonly struct GetPriceOptimizationResponse : IPriceOptimizationResult
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

    public readonly struct GetBatchPriceOptimizationsResponse : IPriceOptimizationResult
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


    public struct PriceOptimization
    {
        public string ItemId { get; set; }
        public string DeviceId { get; set; }
        public string? UserId { get; set; }
        public decimal Price { get; set; }
        public bool IsOptimizedPrice { get; set; }
    }

    public readonly struct GetPriceOptimizationRequest
    {
        public string DeviceId { get; }
        public string ItemId { get; }
        public string? UserId { get; }
        public decimal DefaultPrice { get; }
        public bool OverrideToDefaultPrice { get; }
        public string? UserAgent { get; }

        public GetPriceOptimizationRequest(string deviceId, string itemId, decimal defaultPrice, string? userId = null, bool overrideToDefaultPrice = false, string? userAgent = default)
        {
            DeviceId = deviceId;
            ItemId = itemId;
            UserId = userId;
            DefaultPrice = defaultPrice;
            OverrideToDefaultPrice = overrideToDefaultPrice;
            UserAgent = userAgent;
        }
    }


    public readonly struct GetBatchPriceOptimizationsRequest
    {
        public IEnumerable<GetPriceOptimizationRequest> Requests { get; }
        public string? UserAgent { get; }

        public GetBatchPriceOptimizationsRequest(IEnumerable<GetPriceOptimizationRequest> requests, string? userAgent = default)
        {
            Requests = requests;
            UserAgent = userAgent;
        }
    }


    public enum PriceOptimizationError
    {
        None,
        AuthError,
        BadRequest,
        Timeout,
        Unknown
    }
}