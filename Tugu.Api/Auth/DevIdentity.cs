using System.Security.Claims;
using Tugu.Application.Common.Exceptions;

namespace Tugu.Api.Auth;

/// <summary>
/// Identidad del llamador. Con Cognito activo, sale del JWT (claim "sub" =
/// id del usuario en Cognito, que las apps deben registrar como Id del User
/// al crearlo). Sin Cognito (local), del header X-Dev-UserId.
/// Los controllers solo llaman GetUserId; no saben de dónde viene.
/// </summary>
public static class DevIdentity
{
    public const string HeaderName = "X-Dev-UserId";

    public static Guid GetUserId(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            if (Guid.TryParse(sub, out var fromToken))
                return fromToken;

            throw new UnauthenticatedException("El token no trae un identificador de usuario válido.");
        }

        if (CognitoJwt.IsEnabled(context.RequestServices.GetRequiredService<IConfiguration>()))
            throw new UnauthenticatedException("Falta el token de autenticación (Authorization: Bearer <JWT>).");

        var raw = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var userId))
            throw new UnauthenticatedException(
                $"Falta el header {HeaderName} con un UUID de usuario válido (identidad temporal de desarrollo).");

        return userId;
    }
}
