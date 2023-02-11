using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using JsonClaimValueTypes = Microsoft.IdentityModel.JsonWebTokens.JsonClaimValueTypes;

namespace Spresso.MockApi.Controllers ;

    [ApiController]
    [Route("identity/v1/public")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        [HttpPost("token", Name = "token")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Auth0TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetToken([FromBody] Auth0TokenRequest request, CancellationToken cancellationToken)
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

            var serviceList = new[] { "tms", "pim", "price-optimization" };
            var claims = new List<Claim>();
            if (string.IsNullOrEmpty(request.scope))
            {
                claims.Add(new Claim("scope", "view edit"));
            }
            else
            {
                claims.Add(new Claim("scope", request.scope));
            }
            claims.Add(new Claim("https://api.spresso.com/orgId", "org_FakeOrg"));
            claims.Add(new Claim("https://api.spresso.com/services", JsonSerializer.Serialize(serviceList), JsonClaimValueTypes.JsonArray));
            claims.Add(new Claim("gty", "client-credentials"));
            claims.Add(new Claim("permissions", JsonSerializer.Serialize(claims.Single(c => c.Type == "scope").Value.Split(' ')), JsonClaimValueTypes.JsonArray));
            claims.Add(new Claim("azp", request.client_id!));
            claims.Add(new Claim("sub", request.client_id + "@clients"));


            var jwtPayload = new JwtPayload("https://mock-auth.spresso.com", "https://mock-spresso-api", claims, DateTime.UtcNow,
                DateTime.UtcNow.AddDays(1));

            var jwt =
                new JwtSecurityToken(
                    new JwtHeader(new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("NcQfTjWnZr4u7x!A")), SecurityAlgorithms.HmacSha256)),
                    jwtPayload);

            var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
            var accessToken = jwtSecurityTokenHandler.WriteToken(jwt);
            return Ok(new Auth0TokenResponse
            {
                access_token = accessToken,
                scope = claims.Single(c => c.Type == "scope").Value,
                expires_in = 86400,
                token_type = "Bearer"
            });
        }
    }

    public class Auth0TokenResponse
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
        public string scope { get; set; } = string.Empty;
    }

    public class Auth0TokenRequest
    {
        public string? client_id { get; set; }
        public string? client_secret { get; set; }
        public string? audience { get; set; }
        public string? grant_type { get; set; }
        public string scope { get; set; } = string.Empty;
    }