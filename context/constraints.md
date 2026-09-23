# TUGU Backend — Restricciones y decisiones

Reglas que NINGÚN agente ni tarjeta del Trello puede saltarse. Si una tarjeta
pide lo contrario, se escala al tech-lead y al jefe; no se implementa en
silencio. Fuente original: `CLAUDE.md` (raíz de `D:\trabajo tugu`).

## 1. No negociables (producto financiero con datos biométricos)

1. **Precisión monetaria:** montos siempre `decimal` (`decimal(18,2)` en BD),
   nunca `float` / `double`, ni en el backend ni en el contrato para las apps.
2. **Idempotencia:** toda operación que mueve saldo acepta `idempotencyKey`
   (UUID generado por el cliente) y es segura ante reintentos y dobles
   peticiones. Índice único en BD como red de seguridad.
3. **Atomicidad y concurrencia:** actualizar saldo + registrar transacción ocurre
   dentro de una transacción SQL real con lock de fila (`FOR UPDATE`). Nunca dos
   queries sueltos de "leer saldo" + "escribir saldo". Solo `TransactionEngine`
   escribe saldo.
4. **Datos biométricos sensibles (Ley 1581):** el template se guarda encriptado
   (AES-256, IV por operación). JAMÁS se registra en logs un template (en claro
   o base64), un JWT completo ni una contraseña. Ningún response devuelve el
   template.
5. **Auditoría:** toda entidad lleva `CreatedAt`, `UpdatedAt`, `CreatedBy`.
   `transactions` es inmutable: los errores se corrigen con reversas, nunca
   editando; `BalanceAfter` guarda el saldo resultante.

## 2. Alcance: es un PROTOTIPO/MVP

- Sin usuarios ni dinero real. Objetivo: demostrar el flujo de punta a punta.
- **Prohibido sin pedido explícito:** alta disponibilidad, multi-región,
  Aurora Serverless, RDS Proxy, microservicios, CQRS/event sourcing, observabilidad
  "enterprise", ambientes Staging/Production antes de tener infraestructura.
- Target de infraestructura cuando llegue AWS: RDS PostgreSQL pequeño
  (`db.t3.micro`/`db.t4g.micro`) en `sa-east-1`, API en un solo servicio, Cognito
  para JWT, CDK en C# para la infraestructura. **NO** Aurora ni RDS Proxy en
  esta etapa.
- `reports` sigue fuera del modelo hasta la tarjeta [P1][REPORTS].
- Solo ambiente Development hasta la fase de infraestructura.

## 3. Arquitectura

- Clean Architecture con dependencias `Api → Application/Infrastructure/Contracts`,
  `Application → Domain`, `Infrastructure → Application/Domain`, `Domain → nada`.
- Toda respuesta HTTP va envuelta en `ApiResponse<T>`; los errores usan
  `ApiError { code, message, details }` con códigos estables:
  `VALIDATION_ERROR` 400, `UNAUTHENTICATED` 401, `FORBIDDEN` 403, `NOT_FOUND` 404,
  `CONFLICT` 409, `INSUFFICIENT_FUNDS` 409, `INTERNAL_ERROR` 500.
- Enums con valores numéricos explícitos que no cambian una vez hay datos.
- Cambios de modelo → migración EF + `docs/er-diagram.md`. Cambios de contrato →
  `docs/api-handoff.md` + `docs/openapi.json`.

## 4. Decisiones tomadas (con fecha y estado)

| Fecha | Decisión | Estado |
|---|---|---|
| 2026-08-24 | Pago SOLO con huella (identificación 1:N); sin QR ni documento en el flujo de pago. El documento es dato KYC. | Aceptada (usuario) |
| 2026-08-24 | Sin `password_hash` en `users`: las credenciales viven en Cognito. | Aceptada (usuario) |
| 2026-08-24 | Una huella por usuario. | Aceptada (usuario) |
| 2026-09-06 | El retiro exige usuario/comercio con estado `Active` (KYC verificado); `PendingVerification` puede recargar pero no retirar. Aún no hay endpoint para pasar a `Active`. | Aceptada; revisar con el jefe al hacer Cognito/KYC |
| 2026-09-06 | El retiro exige `deviceId` de un datáfono `Active` (corresponsal). La app Personal no retira. | Aceptada; revisar con el jefe |
| 2026-09-06 | `PUT /users/{id}` solo por el propio usuario; el documento de identidad no es editable por API. | Aceptada |
| 2026-09-06 | `companies` entra al alcance porque es P0 en el Trello (pese a que CLAUDE.md lo dejaba fuera). Un usuario administra un solo comercio. | Aceptada (usuario) |
| 2026-09-06 | El NIT del comercio no es editable por API. | Aceptada |
| 2026-09-06 | La `reference` de una transacción la envía el cliente (no la genera el backend). | Decisión de diseño; ítem "crear referencia" de [RECARGA] queda sin marcar |
| 2026-09-23 | Base en AWS: **RDS PostgreSQL pequeño** (sin Aurora ni RDS Proxy). Migrar a Aurora solo cuando haya usuarios y tráfico real (misma engine PostgreSQL, sin cambios de código). | **Decidida por el jefe**. Al hacer [P0][AWS] ignorar los ítems "RDS Proxy" y "Aurora PostgreSQL Serverless v2" del checklist |
| 2026-09-23 | Rate limiting nativo de .NET 8 por IP (300/min global, 30/min en `/transactions` y `/biometrics`), configurable en `appsettings` → `RateLimiting`. Responde 429 `RATE_LIMITED`. | Aceptada; revisar los umbrales con tráfico real |
| 2026-09-23 | Correlation ID: header `X-Correlation-ID` (lo genera la API si la app no lo manda) devuelto en toda respuesta y presente en cada línea de log de la petición. Las apps deberían enviarlo y guardarlo para soporte. | Aceptada |

## 5. Cosas temporales que NO deben crecer

- `X-Dev-UserId` como identidad: se elimina con Cognito. No agregar lógica de
  autorización sobre el header más allá de "quién soy".
- Clave AES en `appsettings.Development.json`: nunca en otro ambiente; en
  producción sale de un KMS.
- Repositorios `InMemory*`: solo para tests unitarios, nunca registrados en DI
  de la API.
