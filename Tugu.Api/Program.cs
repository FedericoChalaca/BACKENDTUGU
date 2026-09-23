using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tugu.Api.Middleware;
using Tugu.Application;
using Tugu.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Logging estructurado con el proveedor built-in de .NET (JSON en consola).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    options.UseUtcTimestamp = true;
});

// Capas de la aplicación (Clean Architecture).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// API.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Errores de model binding (campos faltantes/mal tipados) también
        // salen en el formato estándar ApiResponse, no en ProblemDetails.
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray());

            var response = Tugu.Contracts.Common.ApiResponse<object>.Fail(
                new Tugu.Contracts.Common.ApiError
                {
                    Code = "VALIDATION_ERROR",
                    Message = "La petición tiene datos inválidos o incompletos.",
                    Details = details
                });

            return new BadRequestObjectResult(response);
        };
    });
// Rate limiting (built-in de .NET 8, sin librerías). Límite por IP para toda
// la API y uno más estricto para operaciones de dinero y biometría. Al
// superarlo responde 429 en el formato estándar. Los valores viven en
// appsettings ("RateLimiting") para ajustarlos por ambiente sin recompilar.
var rl = builder.Configuration.GetSection("RateLimiting");
var globalPerMinute = rl.GetValue("GlobalPerMinute", 300);
var sensitivePerMinute = rl.GetValue("SensitivePerMinute", 30);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("sensitive", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = sensitivePerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        var body = Tugu.Contracts.Common.ApiResponse<object>.Fail(
            "RATE_LIMITED", "Demasiadas peticiones. Espera un momento e intenta de nuevo.");
        await ctx.HttpContext.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), ct);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TUGU API",
        Version = "v1",
        Description = "Backend compartido de TUGU (Personal, Negocios y Datáfono)."
    });
});

var app = builder.Build();

// Orden: correlation ID primero (para que hasta los errores lo lleven), luego
// el manejo global de excepciones, luego el rate limiter.
app.UseMiddleware<RequestContextMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Solo en Development: aplica migraciones pendientes y siembra datos de
    // prueba. En ambientes reales las migraciones se aplican en el despliegue.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Tugu.Infrastructure.Persistence.TuguDbContext>();
    await Tugu.Infrastructure.Persistence.DevDataSeeder.MigrateAndSeedAsync(db);
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
