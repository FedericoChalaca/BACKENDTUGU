# TUGU Backend

Backend único y compartido de TUGU (fintech colombiana de pagos con biometría).
Lo consumen tres apps: **TUGU Personal**, **TUGU Negocios** y **TUGU Datáfono**.

## Stack

- .NET 8 / C# / ASP.NET Core Web API
- Clean Architecture
- PostgreSQL + EF Core *(fase posterior — aún no configurado)*
- Amazon Cognito para autenticación *(fase posterior — aún no configurado)*

## Estructura de la solución

```
Tugu.sln
├── Tugu.Api             → Punto de entrada (Web API). Controllers, middleware, DI, Swagger.
├── Tugu.Application     → Casos de uso, interfaces de repositorios, DTOs, validaciones.
├── Tugu.Domain          → Entidades y lógica de dominio pura. Sin dependencias externas.
├── Tugu.Infrastructure  → Implementaciones concretas (EF Core, servicios externos). Vacío por ahora.
├── Tugu.Contracts       → Contratos de API (ApiResponse<T>, ApiError, request/response models).
└── Tugu.Tests           → Pruebas con xUnit.
```

Reglas de dependencia (Clean Architecture):

```
Api → Application, Infrastructure, Contracts
Application → Domain
Infrastructure → Application, Domain
Tests → todos
```

## Requisitos

- [.NET SDK 8+](https://dotnet.microsoft.com/download) (`dotnet --version` para verificar).
  > Los proyectos apuntan a `net8.0` con `RollForward=LatestMajor`, así que también
  > corren con un runtime .NET 9 instalado.

## Cómo correr el proyecto localmente

Requiere Docker Desktop corriendo (para PostgreSQL local).

```bash
docker compose up -d
dotnet restore
dotnet build
dotnet run --project Tugu.Api
```

Al arrancar en Development, la API aplica las migraciones pendientes y siembra
2 usuarios de prueba (Ana con $50.000 COP y Carlos con $0) si la base está vacía.

Migraciones (herramienta local, no requiere instalación global):

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add NombreDeLaMigracion --project Tugu.Infrastructure --startup-project Tugu.Api
```

La API queda en `http://localhost:5000` (perfil `http` por defecto).

- **Health check:** `GET http://localhost:5000/health`
- **Swagger UI:** `http://localhost:5000/swagger` (solo en Development)

## Endpoints actuales

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/health` | Estado de la API |
| POST | `/users` | Crear usuario |
| GET | `/users/me` | Usuario autenticado |
| POST | `/wallets` | Crear billetera de un usuario |
| GET | `/wallets/me` | Billetera del usuario autenticado |
| POST | `/devices/register` | Registrar un datáfono por serial |
| POST | `/transactions/recharge` | Recargar saldo (idempotente, atómico) |

> **Identidad temporal de desarrollo:** hasta integrar Cognito, los endpoints
> `/me` leen el header `X-Dev-UserId` con el UUID del usuario. Ese header
> desaparece cuando llegue el JWT.
>
> **Persistencia:** PostgreSQL local vía Docker (`docker compose up -d`) con
> EF Core. El modelo de datos está documentado en
> [docs/er-diagram.md](docs/er-diagram.md).

## Cómo correr las pruebas

```bash
dotnet test
```

## Formato estándar de respuesta

Todas las respuestas usan la envoltura `ApiResponse<T>` definida en `Tugu.Contracts`:

```json
// Éxito
{ "success": true, "data": { ... }, "error": null, "timestamp": "2026-08-24T12:00:00Z" }

// Error
{ "success": false, "data": null, "error": { "code": "INTERNAL_ERROR", "message": "..." }, "timestamp": "..." }
```

## Pendiente (fases posteriores)

- Modelo de datos + PostgreSQL/EF Core y migraciones
- Autenticación con Amazon Cognito (JWT por app)
- Entidades de dominio (User, Wallet, Transaction, ...)
- Ambientes de Staging/Production
