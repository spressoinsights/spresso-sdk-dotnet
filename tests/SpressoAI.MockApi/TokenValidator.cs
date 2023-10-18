using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SpressoAI.MockApi;

public static class TokenValidator
{
    public static bool ValidateToken(HttpRequest request)
    {
        var authToken = request.Headers.Authorization.FirstOrDefault(h => h.StartsWith("Bearer"));
        if (authToken == null) return false;
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            tokenHandler.ValidateToken(authToken.Substring("Bearer ".Length),
                new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("NcQfTjWnZr4u7x!A")),
                    ValidIssuer = "https://mock-spresso-auth",
                    ValidAudience = "https://mock-spresso-api"
                }, out _);
        }
        catch
        {
            return false;
        }

        return true;
    }
}