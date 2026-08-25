using System.Text.Json;
using Tugu.Application.Common.Exceptions;
using Tugu.Contracts.Common;

namespace Tugu.Api.Middleware;

/// <summary>
/// Captura cualquier excepción no manejada y devuelve un error 500 con el
/// formato estándar <see cref="ApiResponse{T}"/>. Nunca expone detalles
/// internos al cliente; el detalle completo queda en los logs.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            // Errores de negocio esperados: se traducen al formato estándar
            // con su status HTTP, sin loguearse como error del sistema.
            context.Response.StatusCode = ex switch
            {
                ValidationException => StatusCodes.Status400BadRequest,
                UnauthenticatedException => StatusCodes.Status401Unauthorized,
                NotFoundException => StatusCodes.Status404NotFound,
                ConflictException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            context.Response.ContentType = "application/json";

            var error = new ApiError
            {
                Code = ex.Code,
                Message = ex.Message,
                Details = (ex as ValidationException)?.Details
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(ApiResponse<object>.Fail(error), JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Excepción no manejada procesando {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                code: "INTERNAL_ERROR",
                message: "Ocurrió un error interno. Intenta de nuevo más tarde.");

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
        }
    }
}
