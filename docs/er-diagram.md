# TUGU — Diagrama ER (MVP)

Modelo de datos del MVP. Cinco entidades; `companies` y `reports` quedan
deliberadamente fuera hasta que se pidan.

Convenciones: UUID como PK en todo, timestamps en UTC, montos siempre
`decimal`, auditoría (`created_at`, `updated_at`, `created_by`) en todas
las entidades.

```mermaid
erDiagram
    USERS ||--o| WALLETS : "posee"
    USERS ||--o| BIOMETRICS : "enrola"
    WALLETS ||--o{ TRANSACTIONS : "registra"
    DEVICES |o--o{ TRANSACTIONS : "origina"

    USERS {
        uuid id PK
        int document_type "CC / CE / TI / Passport"
        string document_number "unico junto con document_type (KYC)"
        string first_name
        string last_name
        string phone_number "unico"
        string email "nullable"
        int status "PendingVerification / Active / Blocked"
        timestamptz created_at
        timestamptz updated_at
        string created_by
    }

    WALLETS {
        uuid id PK
        uuid user_id FK "unico: 1 wallet por usuario"
        int owner_type "User (Company en fase posterior)"
        decimal balance "decimal(18,2), nunca float"
        char currency "COP"
        int status "Active / Frozen / Closed"
        timestamptz created_at
        timestamptz updated_at
        string created_by
    }

    TRANSACTIONS {
        uuid id PK
        uuid wallet_id FK
        int type "Recharge (Withdrawal fase 2)"
        decimal amount "positivo; el sentido lo da type"
        decimal balance_after "snapshot para auditoria"
        int status "Pending / Completed / Failed / Reversed"
        uuid idempotency_key "unico: clave de idempotencia"
        string reference "nullable, referencia externa"
        uuid device_id FK "nullable: datafono de origen"
        timestamptz created_at
        string created_by
    }

    BIOMETRICS {
        uuid id PK
        uuid user_id FK "unico: 1 huella por usuario"
        bytes encrypted_template "encriptado en reposo, JAMAS en logs"
        string template_format "formato/SDK del lector"
        int status "Active / Revoked"
        timestamptz enrolled_at "nullable"
        uuid enrolled_device_id "nullable"
        timestamptz created_at
        timestamptz updated_at
        string created_by
    }

    DEVICES {
        uuid id PK
        string serial_number "unico"
        string alias
        int status "Active / Inactive"
        timestamptz last_seen_at "nullable"
        timestamptz created_at
        timestamptz updated_at
        string created_by
    }
```

## Notas de diseño

- **Pago solo con huella (identificación 1:N):** el datáfono captura la huella
  y el backend identifica al usuario contra los templates enrolados. No hay QR
  ni búsqueda por documento en el flujo de pago. El documento existe solo como
  dato de identidad (KYC).
- **Sin `password_hash`:** las credenciales de login vivirán en Amazon Cognito
  (Tarea 1.4). Guardarlas también en nuestra BD duplicaría autenticación.
- **`transactions` es inmutable:** los errores se corrigen con transacciones de
  reversa, nunca editando la original. `balance_after` guarda el saldo
  resultante como evidencia de auditoría.
- **Idempotencia:** `idempotency_key` tendrá índice único; un reintento con la
  misma clave devuelve la transacción original en vez de duplicar el efecto.
