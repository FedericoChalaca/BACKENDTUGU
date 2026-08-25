using Tugu.Application.Common.Exceptions;

namespace Tugu.Api.Auth;

/// <summary>
/// TEMPORAL hasta integrar Cognito (Tarea 1.4): los endpoints "/me" leen la
/// identidad del header X-Dev-UserId. Cuando exista JWT, esta clase se
/// elimina y la identidad sale del token; los controllers no cambian de forma.
/// </summary>
public static class DevIdentity
{
    public const string HeaderName = "X-Dev-UserId";

    public static Guid GetUserId(HttpContext context)
    {
        var raw = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var userId))
            throw new UnauthenticatedException(
                $"Falta el header {HeaderName} con un UUID de usuario válido (identidad temporal de desarrollo).");

        return userId;
    }
}
