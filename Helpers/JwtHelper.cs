using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AppFronton.Helpers;

public static class JwtHelper
{
    public static string GenerateToken(int idUsuario, string correo, IConfiguration config)
    {
        var secret   = config["JWT_SECRET"]!;
        var issuer   = config["JWT_ISSUER"]!;
        var audience = config["JWT_AUDIENCE"]!;
        var hours    = int.Parse(config["JWT_EXPIRES_HOURS"] ?? "24");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   idUsuario.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, correo),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Extrae el id_usuario del JWT del HttpContext.
    /// </summary>
    public static int GetUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(sub, out var id) ? id : 0;
    }
}
