# TUGU Backend — Orquestación del trabajo

Cómo se mueve una tarjeta del Trello por los roles hasta HECHO, y cómo se
invocan los agentes de `.claude/agents/`. El Trello manda las prioridades;
`context/constraints.md` manda los límites.

## 1. Roles y quién hace qué

| Rol (agente) | Entra cuando | Entrega |
|---|---|---|
| **tech-lead** | Al elegir la siguiente tarjeta y al cerrar una | Tarjeta elegida y por qué; veredicto final HECHO/REVISIÓN/BLOQUEADO; resumen para el jefe |
| **architect** | Antes de implementar si la tarjeta toca modelo, capas, migraciones o infraestructura | APROBADO / CON CAMBIOS / RECHAZADO con hallazgos `archivo:línea` |
| **backend-developer** | Implementación | Código + tests + docs + tabla "ítem → evidencia" |
| **qa** | Después de implementar | PASA / PASA CON PENDIENTES / NO PASA con evidencia ejecutada |
| **frontend-integration** | Cuando la tarjeta expone o cambia un endpoint | Handoff y OpenAPI al día; impacto en cada app |

Invocación desde Claude Code (ejemplos):
- "Usa el agente **tech-lead** para decidir qué tarjeta sigue según el Trello."
- "Usa el agente **architect** para revisar el diseño de [P1][REPORTS] antes de implementar."
- "Usa el agente **backend-developer** para implementar [P1][QA] Pruebas financieras."
- "Usa el agente **qa** para verificar la tarjeta [P0][COMPANIES] ítem por ítem."
- "Usa el agente **frontend-integration** para revisar el contrato de /transactions."

## 2. Flujo de una tarjeta (columnas del Trello)

```
Lista de tareas ──(tech-lead elige)──► EN PROCESO ──(qa PASA)──► HECHO
                                          │
                                          ├─(qa PASA CON PENDIENTES externos)──► REVISIÓN
                                          └─(depende de AWS/SDK/jefe)──────────► BLOQUEADO
```

**Paso 0 — Elegir (tech-lead).** Orden: P0 EN PROCESO → P0 en Lista no bloqueadas
→ P1. Mover la tarjeta a EN PROCESO. Leer descripción y checklist completos.

**Paso 1 — Diseño (architect), solo si toca modelo/capas/infra.** Veredicto
antes de escribir código. Un RECHAZADO devuelve al paso 0.

**Paso 2 — Implementar (backend-developer).** Sigue el orden Domain →
Application → Infrastructure → Contracts → Api → Tests → Docs. Una rama no es
obligatoria (equipo de 1); un commit por tarjeta, en español.

**Paso 3 — Verificar (qa).** Suite completa + E2E por HTTP + BD + CI. Tabla
"ítem del checklist → evidencia".

**Paso 4 — Contrato (frontend-integration), si hay endpoints.** Handoff, OpenAPI,
impacto en apps.

**Paso 5 — Cerrar (tech-lead).** Con qa PASA y CI verde: marcar los ítems del
checklist en Trello, pegar la tabla de evidencia como comentario, mover a HECHO,
actualizar `context/project.md` (sección "Estado del Trello"). Registrar
decisiones nuevas en `context/constraints.md`.

## 3. Definición de hecho (DoD)

Una tarjeta está HECHA solo si:

1. Cada ítem del checklist tiene evidencia reproducible (endpoint probado,
   test nombrado o consulta SQL).
2. `dotnet build` sin warnings; `dotnet test` en verde y **cero omitidos**.
3. E2E por HTTP: caso feliz + todos los códigos de error del contrato.
4. Si toca saldo: pruebas de idempotencia (misma key secuencial y simultánea),
   concurrencia (dos operaciones a la vez) y saldo insuficiente.
5. Si toca biometría: logs sin rastro del template.
6. CI en verde en GitHub para el commit.
7. Docs al día: `README.md` (tabla de endpoints), `docs/api-handoff.md`,
   `docs/openapi.json`, `docs/er-diagram.md` si cambió el modelo.

Ítems sin evidencia por bloqueo externo → REVISIÓN con comentario que diga qué
lo desbloquea. Ítems que son recortes deliberados del MVP → HECHO con comentario.

## 4. Patrón de verificación E2E

Script PowerShell contra `http://localhost:5000` con una función `Check(nombre,
condición)` que acumula OK/FAIL, una `Call(método, ruta, body, headers)` que
devuelve `{Status, Json}` incluso en errores HTTP, y consultas
`docker exec tugu-postgres psql -U tugu -d tugu -t -A -c '...'` para comprobar
persistencia. Termina con `RESULTADO: N OK, M FAIL` y `exit 1` si hay fallos.
Cubrir por endpoint: 2xx esperado, 400 (validación y body incompleto), 401 (sin
identidad), 403 (identidad ajena), 404 (id inexistente), 409 (duplicado/estado).
Referencia de cobertura actual: base 34, biometría 16, users/wallets/devices/
retiro/movimientos 32, companies 30 = 112 verificaciones.

## 5. Convenciones

- **Commits:** un commit por tarjeta, título ≤ 72 caracteres en español, cuerpo
  con lista de cambios y n.º de tests/E2E. Push a `main` solo con suite verde.
- **Nombres:** módulos en `Tugu.Application/<Modulo>/`, contratos en
  `Tugu.Contracts/<Modulo>/`, tests en `Tugu.Tests/Application/` (unitarios) y
  `Tugu.Tests/Transactions/` (integración con Postgres).
- **Trello:** comentario en la tarjeta al cerrarla con la tabla de evidencia;
  comentario en BLOQUEADO con la dependencia exacta.
- **Cuando algo contradice `CLAUDE.md` / `context/constraints.md`:** se escribe
  en la fila "PENDIENTE del jefe" de `context/constraints.md` y no se implementa.

## 6. Plan por fases (estado 2026-09-07)

| Fase | Tarjetas | Estado | Desbloqueo |
|---|---|---|---|
| Cimientos + módulos + motor + biometría | 13 tarjetas P0 | HECHO | — |
| Handoff | [P0][HANDOFF] | REVISIÓN 11/14 | Cuenta AWS (URL DEV, JWT, credenciales) |
| Pipeline | [P0][DEVOPS] | EN PROCESO 2/11 | Cuenta AWS (deploy DEV/STAGING) |
| Auth + infraestructura + integración + release | AUTH, AWS, INTEGRATION ×2, RELEASE | BLOQUEADO | Cuenta AWS + decisión Aurora vs RDS + avance de las apps |
| P1 sin bloqueo | [QA] Pruebas financieras → [SECURITY] Seguridad + observabilidad (parte local) → [REPORTS] | Lista de tareas | Ninguno: se pueden hacer ya |

Orden sugerido mientras llega AWS: **[P1][QA]** (formalizar las pruebas
financieras como suite nombrada y reporte), **[P1][SECURITY]** (rate limiting,
headers de seguridad, redacción de logs, health detallado) y **[P1][REPORTS]**
(agregados por billetera/comercio sobre `transactions`; requiere diseño del
architect porque agrega consultas y quizá tabla `reports`).
