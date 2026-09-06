# TUGU Backend — Contrato de API para las apps (handoff)

Documento para el equipo de las apps KMP (Personal, Negocios, Datáfono).
Describe cómo hablarle al backend: URL, autenticación, formato de respuestas,
códigos de error, endpoints con ejemplos y usuarios de prueba.

La fuente de verdad siempre es el **Swagger** de la API en ejecución:
`GET /swagger` (UI) y `GET /swagger/v1/swagger.json` (OpenAPI 3). Una copia
exportada vive en [openapi.json](openapi.json).

## 1. URLs

| Ambiente | Base URL | Estado |
|---|---|---|
| Local (desarrollador) | `http://localhost:5000` | Disponible (`docker compose up -d` + `dotnet run --project Tugu.Api`) |
| DEV (AWS) | *pendiente* | Se define cuando exista la cuenta AWS (tarjeta `[P0][AWS]`) |

## 2. Autenticación

**Estado actual (temporal):** hasta integrar Amazon Cognito, los endpoints que
necesitan saber "quién eres" (`/users/me`, `/wallets/me`, `PUT /users/{id}`,
`GET /transactions` sin `walletId`) leen la identidad del header:

```
X-Dev-UserId: <uuid del usuario>
```

**Estado final (Cognito, tarjeta `[P0][AUTH]`):** ese header desaparece y se
reemplaza por `Authorization: Bearer <JWT de Cognito>`. Los contratos de
request/response NO cambian; solo cambia cómo se envía la identidad. Diseñen
el cliente HTTP con un interceptor de auth intercambiable.

## 3. Formato estándar de respuesta

**Toda** respuesta, exitosa o de error, viene envuelta así:

```json
{
  "success": true,
  "data": { ... },
  "error": null,
  "timestamp": "2026-09-06T14:10:00.000Z"
}
```

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Datos de usuario inválidos.",
    "details": { "phoneNumber": ["El teléfono es obligatorio."] }
  },
  "timestamp": "2026-09-06T14:10:00.000Z"
}
```

- `error.code` es **estable y para máquinas** (úsenlo para decidir comportamiento).
- `error.message` es para mostrar a humanos (en español).
- `error.details` es opcional: errores por campo en validaciones.

Los listados paginados van dentro de `data`:

```json
{ "items": [ ... ], "page": 1, "pageSize": 20, "totalCount": 57, "totalPages": 3 }
```

## 4. Códigos de error

| HTTP | `error.code` | Cuándo |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Campos faltantes, tipos inválidos, reglas de formato |
| 401 | `UNAUTHENTICATED` | Falta identidad (header / token) |
| 403 | `FORBIDDEN` | Identidad válida pero sin permiso (ej. editar otro perfil) |
| 404 | `NOT_FOUND` | Usuario, billetera, dispositivo o transacción inexistente |
| 409 | `CONFLICT` | Duplicados (documento, teléfono, serial), estado inválido (billetera/datáfono inactivo, usuario bloqueado o sin verificar), `idempotencyKey` reutilizada con otros parámetros |
| 409 | `INSUFFICIENT_FUNDS` | Retiro mayor al saldo |
| 500 | `INTERNAL_ERROR` | Error no controlado (nunca expone detalles internos) |

## 5. Endpoints

Convenciones: ids son UUID; fechas en UTC ISO-8601; montos `decimal` en COP
(nunca float en el cliente tampoco: usen `BigDecimal`/equivalente en KMP).

### Salud

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/health` | `{ "status": "Healthy", "timestamp": ... }` |

### Usuarios (TUGU Personal)

| Método | Ruta | Identidad | Descripción |
|---|---|---|---|
| POST | `/users` | no | Registro. `201` |
| GET | `/users/me` | sí | Perfil propio |
| GET | `/users/{id}` | no | Perfil por id |
| PUT | `/users/{id}` | sí, debe ser el mismo id | Edita nombre/teléfono/email (el documento no es editable) |

`POST /users` request:
```json
{ "documentType": 1, "documentNumber": "1017654321", "firstName": "Ana", "lastName": "Gómez",
  "phoneNumber": "3001234567", "email": "ana@correo.com" }
```
`documentType`: `1`=CC, `2`=CE, `3`=TI, `4`=Passport.

Response `data`:
```json
{ "id": "…", "documentType": "CC", "documentNumber": "1017654321", "firstName": "Ana", "lastName": "Gómez",
  "phoneNumber": "3001234567", "email": "ana@correo.com", "status": "PendingVerification", "createdAt": "…" }
```
`status`: `PendingVerification` (recién registrado: puede recargar, **no** retirar), `Active` (KYC verificado), `Blocked`.

`PUT /users/{id}` request (todos opcionales, al menos uno):
```json
{ "firstName": "Anita", "lastName": null, "phoneNumber": null, "email": "nuevo@correo.com" }
```

### Comercios (TUGU Negocios)

| Método | Ruta | Identidad | Descripción |
|---|---|---|---|
| POST | `/companies` | sí | Crea el comercio; el usuario autenticado queda como miembro. `201` |
| GET | `/companies/me` | sí | Comercio del usuario autenticado (`404` si no pertenece a ninguno) |
| GET | `/companies/{id}` | no | Comercio por id |
| PUT | `/companies/{id}` | sí, miembro | Edita nombre/email/teléfono. El NIT no es editable |
| POST | `/companies/{id}/members` | sí, miembro | Asocia otro usuario (`409` si ya pertenece a un comercio) |

`POST /companies` request:
```json
{ "name": "Tienda Belén", "nit": "900.123.456-7", "email": "tienda@correo.com", "phoneNumber": "6041234567" }
```
El NIT se acepta con o sin puntos y se guarda normalizado (`900123456-7`).

Response `data`:
```json
{ "id": "…", "name": "Tienda Belén", "nit": "900123456-7", "email": "…", "phoneNumber": "…",
  "status": "PendingVerification", "memberUserIds": ["…"], "walletId": null, "createdAt": "…" }
```
`status`: `PendingVerification` (puede recargar, **no** retirar), `Active`, `Blocked`.
Un usuario administra como máximo un comercio.

### Billeteras

| Método | Ruta | Identidad | Descripción |
|---|---|---|---|
| POST | `/wallets` | solo para comercio | Crea la billetera de un usuario (`userId`) o de un comercio (`companyId`, solo miembros). Una por dueño. `201` |
| GET | `/wallets/me` | sí | Billetera propia del usuario (incluye saldo) |
| GET | `/wallets/{id}` | no | Billetera por id (usuario o comercio) |

`POST /wallets` request: `{ "userId": "…" }` **o** `{ "companyId": "…" }` (exactamente uno).
Response `data`:
```json
{ "id": "…", "ownerType": "User", "userId": "…", "companyId": null, "balance": 0,
  "currency": "COP", "status": "Active", "createdAt": "…" }
```
La billetera del comercio se obtiene con `GET /companies/me` (campo `walletId`) y luego `GET /wallets/{id}`.

### Datáfonos (TUGU Datáfono)

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/devices/register` | Registra por serial. `201` |
| GET | `/devices/{id}` | Detalle (incluye `lastSeenAt`) |
| POST | `/devices/{id}/heartbeat` | Señal de vida; actualiza `lastSeenAt` |
| POST | `/devices/{id}/activate` | Habilita para operar (`409` si ya estaba activo) |
| POST | `/devices/{id}/deactivate` | Inhabilita (`409` si ya estaba inactivo) |
| PUT | `/devices/{id}/company` | Asigna el datáfono a un comercio (corresponsal) |

`POST /devices/register` request: `{ "serialNumber": "SN-000123", "alias": "Datáfono tienda Belén", "companyId": "…" }` (`companyId` opcional).
Response `data`: `{ "id": "…", "serialNumber": "SN-000123", "alias": "…", "status": "Active", "companyId": "…", "lastSeenAt": "…", "createdAt": "…" }`.

### Transacciones

| Método | Ruta | Identidad | Descripción |
|---|---|---|---|
| POST | `/transactions/recharge` | no | Recarga. `201` nueva / `200` replay idempotente |
| POST | `/transactions/withdraw` | no | Retiro en datáfono. `201` / `200` replay |
| GET | `/transactions` | sí (si no envían `walletId`) | Movimientos paginados |
| GET | `/transactions/{id}` | no | Detalle |

**Idempotencia (obligatoria):** el cliente genera un UUID `idempotencyKey`
**antes** de enviar y lo persiste localmente. Si hay timeout o error de red,
**reintenta con la misma clave**: el backend responde `200` con la transacción
original y `wasReplay: true`, sin mover saldo dos veces. Generar una clave nueva
en cada reintento duplicaría la operación.

`POST /transactions/recharge` request:
```json
{ "walletId": "…", "amount": 50000, "idempotencyKey": "b4c0…", "reference": "recibo-123", "deviceId": null }
```
`deviceId` es opcional en recarga (puede venir de la app).

`POST /transactions/withdraw` request:
```json
{ "walletId": "…", "amount": 20000, "idempotencyKey": "9f1a…", "reference": "caja-1", "deviceId": "…" }
```
`deviceId` es **obligatorio** en retiro (el efectivo lo entrega un datáfono activo).
Reglas: billetera `Active`, usuario `Active` (verificado), datáfono `Active`, saldo suficiente.

Response `data` (ambos):
```json
{ "id": "…", "walletId": "…", "type": "Recharge", "amount": 50000, "balanceAfter": 125000,
  "status": "Completed", "idempotencyKey": "…", "reference": "recibo-123", "deviceId": null,
  "createdAt": "…", "wasReplay": false }
```

`GET /transactions` query params (todos opcionales):

| Param | Valores | Default |
|---|---|---|
| `walletId` | uuid | billetera del usuario autenticado |
| `type` | `Recharge` (entrada), `Withdrawal` (salida) | todos |
| `status` | `Pending`, `Completed`, `Failed`, `Reversed` | todos |
| `from`, `to` | fecha/hora UTC ISO-8601, inclusivas | sin límite |
| `sort` | `desc` (más reciente primero), `asc` | `desc` |
| `page` | ≥ 1 | 1 |
| `pageSize` | 1–100 | 20 |

### Biometría (TUGU Datáfono / Personal)

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/biometrics/enroll` | Enrola la huella de un usuario (una por usuario). `201` |
| POST | `/biometrics/verify` | Identifica al usuario **solo con la huella** (1:N). Siempre `200` |
| GET | `/biometrics/status/{userId}` | `NotEnrolled` / `Active` / `Revoked` |

`POST /biometrics/enroll` request:
```json
{ "userId": "…", "templateBase64": "<template del SDK en base64>", "templateFormat": "ISO-19794-2", "deviceId": "…" }
```
`POST /biometrics/verify` request: `{ "templateBase64": "…", "deviceId": "…" }` — **sin userId**.
Response `data`: `{ "matched": true, "userId": "…", "firstName": "Ana", "lastName": "Gómez" }` o `{ "matched": false }`.

El template **nunca** vuelve en ninguna respuesta ni se registra en logs.
> Pendiente de confirmar con el SDK del lector: hoy el backend compara el
> template tal cual lo entrega el SDK. Si el SDK hace el matching en el
> dispositivo, el contrato se ajusta (ver nota en `docs/er-diagram.md`).

## 6. Usuarios de prueba (seed de desarrollo)

Se crean automáticamente al arrancar la API en Development si la base está vacía:

| Usuario | Documento | Teléfono | Estado | Saldo inicial |
|---|---|---|---|---|
| Ana Prueba | CC 1017000001 | 3000000001 | `Active` (puede retirar) | $50.000 COP |
| Carlos Prueba | CC 1017000002 | 3000000002 | `Active`; administra "Tienda Prueba" | $0 |

Comercio de prueba: **Tienda Prueba** (NIT `900123456-7`, `Active`, con billetera en $0)
y su datáfono `SN-SEED-0001` (`Active`, asignado a la tienda).

Sus UUIDs se generan en cada base nueva: consúltenlos con `GET /users/{id}`
tras crearlos, o vía Adminer (`http://localhost:8081`, servidor `postgres`,
usuario/base `tugu`).

## 7. Flujos de punta a punta

**Registro y recarga (Personal):**
`POST /users` → `POST /wallets` → `POST /transactions/recharge` → `GET /wallets/me` → `GET /transactions`.

**Enrolamiento (Datáfono/Personal):**
`POST /biometrics/enroll` → `GET /biometrics/status/{userId}`.

**Pago solo con huella (Datáfono):**
`POST /devices/{id}/heartbeat` → `POST /biometrics/verify` (obtiene `userId`) →
`GET /wallets/{id}` o buscar billetera del usuario → `POST /transactions/withdraw`
(o `recharge`) con `deviceId` del datáfono y `idempotencyKey` nueva.

**Comercio (Negocios):**
`POST /companies` (con identidad del dueño) → `POST /wallets { companyId }` → `POST /devices/register { companyId }` →
`GET /companies/me` → `GET /transactions?walletId=<walletId del comercio>`.

## 8. Lo que aún NO existe (para que no lo esperen)

- Cognito/JWT (identidad temporal por header).
- Verificación de identidad/KYC: hoy no hay endpoint para pasar un usuario o comercio a `Active` (llega con Cognito/KYC).
- Reportes/agregaciones (P1).
- Ambientes DEV/STAGING en AWS.
