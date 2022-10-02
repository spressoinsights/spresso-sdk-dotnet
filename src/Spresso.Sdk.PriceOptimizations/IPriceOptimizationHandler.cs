using System.Threading;
using System.Threading.Tasks;

namespace Spresso.Sdk.PriceOptimizations
{
    public interface IPriceOptimizationHandler
    {
        Task<GetPriceOptimizationsResponse> GetPriceOptimizationAsync(GetPriceOptimizationsRequest request, CancellationToken cancellationToken = default);
    }

    public interface IPriceOptimizationResult
    {
        public bool IsSuccess { get; }
        public PriceOptimizationError Error { get; }

    }
    public readonly struct GetPriceOptimizationsResponse : IPriceOptimizationResult
    {
        public bool IsSuccess => Error == PriceOptimizationError.None;
        public PriceOptimization? PriceOptimization { get; }
        public PriceOptimizationError Error { get; }

        public GetPriceOptimizationsResponse(PriceOptimization priceOptimization)
        {
            PriceOptimization = priceOptimization;
            Error = PriceOptimizationError.None;
        }

        public GetPriceOptimizationsResponse(PriceOptimizationError error)
        {
            PriceOptimization = null;
            Error = error;
        }
    }

    public struct PriceOptimization
    {
        public string ItemId { get; set; }
        public string DeviceId { get; set; }
        public string UserId { get; set; }
        public decimal Price { get; set; }
        public bool IsOptimizedPrice { get; set; }
    }

    public struct GetPriceOptimizationsRequest
    {
        public string DeviceId { get; }
        public string ItemId { get; }
        public string? UserId { get; }
        public decimal DefaultPrice { get; }
        public bool OverrideToDefaultPrice { get; }

        public GetPriceOptimizationsRequest(string deviceId, string itemId, decimal defaultPrice, string? userId = null, bool overrideToDefaultPrice = false)
        {
            DeviceId = deviceId;
            ItemId = itemId;
            DefaultPrice = defaultPrice;
            UserId = userId;
            OverrideToDefaultPrice = overrideToDefaultPrice;
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