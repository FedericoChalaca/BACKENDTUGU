# TUGU Backend — Contexto del proyecto

Estado vivo del backend. Actualízalo cuando una tarjeta cambie de columna.
Para el contexto de negocio completo ver `CLAUDE.md` (raíz de `D:\trabajo tugu`).

## Qué es

Backend único (.NET 8, C#, Clean Architecture, PostgreSQL + EF Core) que
consumen tres apps móviles KMP por HTTP: **TUGU Personal** (usuario final),
**TUGU Negocios** (comercio) y **TUGU Datáfono** (dispositivo físico con lector
de huella). Meta del MVP: registrar usuario → enrolar huella → identificar SOLO
con la huella en el datáfono (1:N, sin QR) → recargar/retirar → ver saldo.

Equipo: 1 desarrollador backend (Federico) + supervisor técnico que migra las
apps a KMP. Etapa: **prototipo/MVP**, sin usuarios ni dinero real.

## Dónde está todo

| Qué | Dónde |
|---|---|
| Repositorio | https://github.com/FedericoChalaca/BACKENDTUGU (rama `main`) |
| Tablero | Trello "tugu" (export en `D:\trabajo tugu\NZUCVi5s - tugu.json`) |
| Correr local | `docker compose up -d` → `dotnet run --project Tugu.Api` → `http://localhost:5000/swagger` |
| Base local | PostgreSQL 16 en Docker (`tugu-postgres`), visor Adminer en `http://localhost:8081` |
| Tests | `dotnet test Tugu.sln` (81 tests: unitarios + integración contra Postgres) |
| CI | `.github/workflows/ci.yml` (build + tests con Postgres efímero en cada push) |
| Contrato para las apps | `docs/api-handoff.md` + `docs/openapi.json` |
| Modelo de datos | `docs/er-diagram.md` |
| Agentes de trabajo | `.claude/agents/` (architect, backend-developer, qa, frontend-integration, tech-lead) |
| Flujo de trabajo | `orchestration/workflow.md` |

## Estructura de la solución

```
Tugu.Api             → controllers, middleware de errores, Swagger, DevIdentity (temporal)
Tugu.Application     → servicios (casos de uso), interfaces de repositorio, excepciones de negocio
Tugu.Domain          → entidades y enums, sin dependencias externas
Tugu.Infrastructure  → EF Core (TuguDbContext, migraciones, repositorios), motor transaccional, cifrado AES
Tugu.Contracts       → requests/responses de la API, ApiResponse<T>, ApiError, PagedResponse<T>
Tugu.Tests           → xUnit (unitarios con repos in-memory; integración con SkippableFact)
```

## Módulos y endpoints (24 rutas)

- **Users:** `POST /users`, `GET /users/me`, `GET /users/{id}`, `PUT /users/{id}`
- **Companies:** `POST /companies`, `GET /companies/me`, `GET /companies/{id}`, `PUT /companies/{id}`, `POST /companies/{id}/members`
- **Wallets:** `POST /wallets` (usuario o comercio), `GET /wallets/me`, `GET /wallets/{id}`
- **Devices:** `POST /devices/register`, `GET /devices/{id}`, `POST /devices/{id}/heartbeat`, `POST /devices/{id}/activate|deactivate`, `PUT /devices/{id}/company`
- **Transactions:** `POST /transactions/recharge`, `POST /transactions/withdraw`, `GET /transactions`, `GET /transactions/{id}`
- **Biometrics:** `POST /biometrics/enroll`, `POST /biometrics/verify`, `GET /biometrics/status/{userId}`
- **Health:** `GET /health`

## Estado del Trello (backend) — 2026-09-07

**HECHO (13 P0):** CORE (17/18), DB diseño, DB Postgres+EF, USERS (11/12),
COMPANIES, WALLETS, DEVICES, motor de transacciones, RECARGA (11/12),
MOVIMIENTOS, RETIRO, BIOMETRICS. Los ítems sin marcar de CORE/USERS/RECARGA
están bloqueados por AWS o son decisiones de diseño, no trabajo pendiente.

**REVISIÓN:** HANDOFF (11/14) — falta URL DEV, JWT y credenciales DEV (AWS).

**EN PROCESO:** DEVOPS (2/11) — pipeline y tests listos; deploy DEV/STAGING,
secrets, rollback y smoke tests esperan AWS.

**BLOQUEADO (por cuenta AWS):** AUTH (Cognito), AWS DEV, INTEGRATION Personal,
INTEGRATION Negocios+Datáfono, RELEASE.

**P1 pendientes (sin bloqueo):** [QA] Pruebas financieras, [SECURITY] Seguridad
+ observabilidad, [REPORTS] Reportes.

## Piezas temporales (se reemplazan, no se extienden)

- **Identidad:** header `X-Dev-UserId` (`Tugu.Api/Auth/DevIdentity.cs`) hasta Cognito.
- **Clave AES de biometría:** en `appsettings.Development.json`; en producción vendrá de un KMS.
- **Verificación KYC:** no existe endpoint para pasar usuario/comercio a `Active`; llega con Cognito/KYC.

## Inputs externos pendientes

- Cuenta de AWS (desbloquea AUTH, AWS, DEVOPS deploy, HANDOFF URL DEV, INTEGRATION, RELEASE).
- SDK del lector de huella del datáfono (para calzar el contrato de `verify`).
- Decisión del jefe sobre la contradicción Aurora/RDS Proxy/Lambda vs `CLAUDE.md`.
- Arenera (sandbox) de la Superintendencia Financiera: revisar cuando el usuario lo pida.
