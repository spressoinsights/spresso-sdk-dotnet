using Microsoft.AspNetCore.Mvc;

namespace Spresso.MockApi.Controllers ;

    [ApiController]
    [Route("v1")]
    public class PriceOptimizationsController : Controller
    {
        [HttpGet("priceOptimizations")]
        public async Task<IActionResult> GetSinglePriceOptimization([FromQuery] GetSinglePriceOptimizationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Request.Query.ContainsKey("status"))
            {
                return StatusCode(int.Parse(Request.Query["status"]));
            }
            if (Request.Query.ContainsKey("delay"))
            {
                var delay = int.Parse(Request.Query["delay"]);
                await Task.Delay(new TimeSpan(0, 0, 0, delay), cancellationToken);
            }

            if (request.OverrideToDefaultPrice || Random.Shared.Next(10) >= 7)
            {
                return Ok(new
                {
                    data = new PriceOptimization(request.ItemId, request.DeviceId, request.DefaultPrice, false, request.UserId)
                });
            }

            var defaultPriceInt = (int)(request.DefaultPrice * 100);
            var rangeInt = (int)(0.1m * defaultPriceInt);

            var price = Random.Shared.Next(defaultPriceInt - rangeInt, defaultPriceInt + rangeInt) / 100m;
            return Ok(new
            {
                data = new PriceOptimization(request.ItemId, request.DeviceId, price, true, request.UserId)
            });
        }

        [HttpPost("priceOptimizations")]
        public async Task<IActionResult> GetBatchPriceOptimizations([FromBody] GetBatchPriceOptimizationsRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Request.Query.ContainsKey("status"))
            {
                return StatusCode(int.Parse(Request.Query["status"]));
            }
            if (Request.Query.ContainsKey("delay"))
            {
                var delay = int.Parse(Request.Query["delay"]);
                await Task.Delay(new TimeSpan(0, 0, 0, delay), cancellationToken);
            }


            if (request.PricingRefs.Length > 100)
            {
                return BadRequest("Batch size cannot be greater than 100");
            }

            var response = new List<PriceOptimization>(request.PricingRefs.Length);
            foreach (var pricingRef in request.PricingRefs)
                if (pricingRef.OverrideToDefaultPrice || Random.Shared.Next(10) >= 7)
                {
                    response.Add(new PriceOptimization(pricingRef.ItemId, pricingRef.DeviceId, pricingRef.DefaultPrice, false, pricingRef.UserId));
                }
                else
                {
                    var defaultPriceInt = (int)(pricingRef.DefaultPrice * 100);
                    var rangeInt = (int)(0.1m * defaultPriceInt);

                    var price = Random.Shared.Next(defaultPriceInt - rangeInt, defaultPriceInt + rangeInt) / 100m;
                    response.Add(new PriceOptimization(pricingRef.ItemId, pricingRef.DeviceId, price, true, pricingRef.UserId));
                }

            return Ok(new
            {
                Data = response
            });
        }

        public class GetBatchPriceOptimizationsRequest
        {
            public GetSinglePriceOptimizationRequest[] PricingRefs { get; set; }
        }

        public class GetSinglePriceOptimizationRequest
        {
            public string ItemId { get; set; }
            public string DeviceId { get; set; }
            public string? UserId { get; set; } = default;
            public decimal DefaultPrice { get; set; }
            public bool OverrideToDefaultPrice { get; set; }
        }

        public record PriceOptimization(string ItemId, string DeviceId, decimal Price, bool IsOptimizedPrice, string? UserId = default);
    }