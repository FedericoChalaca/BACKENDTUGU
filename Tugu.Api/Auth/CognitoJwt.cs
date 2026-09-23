using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Tugu.Api.Auth;

/// <summary>
/// Validación de JWT emitidos por Amazon Cognito. Se activa con
/// <c>Cognito:Enabled=true</c> más <c>Cognito:Region</c>, <c>Cognito:UserPoolId</c>
/// y <c>Cognito:ClientIds</c> (uno por app, separados por coma). Sin esos
/// valores (local) la API sigue usando el header X-Dev-UserId.
///
/// Cognito publica sus llaves en {issuer}/.well-known/jwks.json; el middleware
/// las descarga y cachea solo, así que la validación de firma, expiración,
/// issuer y audience es completa sin código propio.
/// </summary>
public static class CognitoJwt
{
    public static bool IsEnabled(IConfiguration config) => config.GetValue("Cognito:Enabled", false);

    public static IServiceCollection AddCognitoJwt(this IServiceCollection services, IConfiguration config)
    {
        if (!IsEnabled(config)) return services;

        var region = config["Cognito:Region"] ?? throw new InvalidOperationException("Falta Cognito:Region.");
        var userPoolId = config["Cognito:UserPoolId"] ?? throw new InvalidOperationException("Falta Cognito:UserPoolId.");
        var clientIds = (config["Cognito:ClientIds"] ?? throw new InvalidOperationException("Falta Cognito:ClientIds."))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var issuer = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = issuer;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = issuer,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // Cognito pone el client id en "aud" (id token) o "client_id" (access token).
                    ValidateAudience = false,
                    AudienceValidator = null
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ctx =>
                    {
                        var aud = ctx.Principal?.FindFirst("aud")?.Value ?? ctx.Principal?.FindFirst("client_id")?.Value;
                        if (aud is null || !clientIds.Contains(aud))
                            ctx.Fail("El token no pertenece a una app de TUGU.");
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization();

        return services;
    }
}
