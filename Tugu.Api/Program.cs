using Microsoft.AspNetCore.Mvc;
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

// Manejo global de excepciones: siempre el primer middleware del pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

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
