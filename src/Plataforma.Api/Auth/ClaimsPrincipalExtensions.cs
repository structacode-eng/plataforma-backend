using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Plataforma.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Lê o id do usuário do claim `sub` (ou NameIdentifier, se houver mapeamento).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
