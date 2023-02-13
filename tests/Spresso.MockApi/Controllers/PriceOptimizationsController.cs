using Microsoft.AspNetCore.Mvc;

namespace Spresso.MockApi.Controllers;

[ApiController]
[Route("pim/v1")]
public class PriceOptimizationsController : Controller
{
    [HttpGet("priceOptimizations")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Response<PriceOptimization>), 200)]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSinglePriceOptimization([FromQuery] GetSinglePriceOptimizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Request.Query.ContainsKey("status")) return StatusCode(int.Parse(Request.Query["status"]));
        if (Request.Query.ContainsKey("delay"))
        {
            var delay = int.Parse(Request.Query["delay"]);
            await Task.Delay(new TimeSpan(0, 0, 0, delay), cancellationToken);
        }


        if (!TokenValidator.ValidateToken(Request))
            return Unauthorized();


        if (request.OverrideToDefaultPrice)
            return Ok(new Response<PriceOptimization>(new PriceOptimization(request.ItemId, request.DeviceId,
                request.DefaultPrice, false, request.UserId)));

        var defaultPriceInt = (int)(request.DefaultPrice * 100);
        var rangeInt = (int)(0.1m * defaultPriceInt);

        var price = Random.Shared.Next(defaultPriceInt - rangeInt, defaultPriceInt + rangeInt) / 100m;
        return Ok(new
        {
            data = new PriceOptimization(request.ItemId, request.DeviceId, price, true, request.UserId)
        });
    }

    [HttpPost("priceOptimizations")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Response<PriceOptimization[]>), 200)]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBatchPriceOptimizations([FromBody] GetBatchPriceOptimizationsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Request.Query.ContainsKey("status")) return StatusCode(int.Parse(Request.Query["status"]));
        if (Request.Query.ContainsKey("delay"))
        {
            var delay = int.Parse(Request.Query["delay"]);
            await Task.Delay(new TimeSpan(0, 0, 0, delay), cancellationToken);
        }


        if (!TokenValidator.ValidateToken(Request))
            return Unauthorized();

        
        if (request.Items.Length > 500) return BadRequest("Batch size cannot be greater than 500");

        var response = new List<PriceOptimization>(request.Items.Length);
        foreach (var pricingRef in request.Items)
            if (pricingRef.OverrideToDefaultPrice)
            {
                response.Add(new PriceOptimization(pricingRef.ItemId, pricingRef.DeviceId, pricingRef.DefaultPrice,
                    false, pricingRef.UserId));
            }
            else
            {
                var defaultPriceInt = (int)(pricingRef.DefaultPrice * 100);
                var rangeInt = (int)(0.1m * defaultPriceInt);

                var price = Random.Shared.Next(defaultPriceInt - rangeInt, defaultPriceInt + rangeInt) / 100m;
                response.Add(new PriceOptimization(pricingRef.ItemId, pricingRef.DeviceId, price, true,
                    pricingRef.UserId));
            }

        return Ok(new Response<List<PriceOptimization>>(response));
    }

    [HttpGet("priceOptimizationOrgConfig")]
    public async Task<IActionResult> GetPriceOptimizationOrgConfig(CancellationToken cancellationToken = default)
    {
        if (!TokenValidator.ValidateToken(Request))
            return Unauthorized();


        return Ok(new
        {
            Data = new
            {
                OrgId = "org_FakeOrg",
                UserAgentBlacklist = new[]
                {
                    new UserAgentBlacklistItem("Googlebot", false, "Googlebot+", 0),
                    new UserAgentBlacklistItem("Storebot-Google", false, "Storebot-Google+", 9)
                }
            }
        });
    }

    public class GetBatchPriceOptimizationsRequest
    {
        public GetSinglePriceOptimizationRequest[] Items { get; set; }
    }

    public class GetSinglePriceOptimizationRequest
    {
        public string ItemId { get; set; }
        public string DeviceId { get; set; }
        public string? UserId { get; set; } = default;
        public decimal DefaultPrice { get; set; }
        public bool OverrideToDefaultPrice { get; set; }
    }

    public record PriceOptimization(string ItemId, string DeviceId, decimal Price, bool isPriceOptimized,
        string? UserId = default);


    public record UserAgentBlacklistItem(string Name, bool IsDefault, string Regexp, int Status);

    public record Response<T>(T Data);
}