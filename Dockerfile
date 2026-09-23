# Imagen de la API para desplegar en AWS (App Runner / ECS) o en cualquier host
# con Docker. Build multi-etapa: compila con el SDK, corre solo con el runtime.
#
#   docker build -t tugu-api .
#   docker run -p 8080:8080 -e ConnectionStrings__TuguDb="..." -e Biometrics__EncryptionKey="..." tugu-api

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restaurar primero (cachea las dependencias entre builds).
COPY Tugu.sln .
COPY Tugu.Api/Tugu.Api.csproj Tugu.Api/
COPY Tugu.Application/Tugu.Application.csproj Tugu.Application/
COPY Tugu.Domain/Tugu.Domain.csproj Tugu.Domain/
COPY Tugu.Infrastructure/Tugu.Infrastructure.csproj Tugu.Infrastructure/
COPY Tugu.Contracts/Tugu.Contracts.csproj Tugu.Contracts/
COPY Tugu.Tests/Tugu.Tests.csproj Tugu.Tests/
RUN dotnet restore Tugu.Api/Tugu.Api.csproj

COPY . .
RUN dotnet publish Tugu.Api/Tugu.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Usuario sin privilegios, puerto no privilegiado.
RUN adduser --disabled-password --gecos "" tugu && chown -R tugu /app
USER tugu
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Tugu.Api.dll"]
