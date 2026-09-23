# Infraestructura AWS (CDK en C#)

Ambiente **DEV** del backend TUGU. Decisión del jefe (2026-09-23): **RDS
PostgreSQL pequeño**, sin Aurora ni RDS Proxy. Prototipo: un solo servicio.

## Qué crea `cdk deploy TuguDev`

| Recurso | Detalle | Costo aprox. (sa-east-1) |
|---|---|---|
| VPC | 2 AZ, subred pública + subred aislada para la base, **sin NAT** | 0 |
| RDS PostgreSQL 16 | `db.t4g.micro`, 20 GB, single-AZ, backups 7 días, cifrado KMS | ~USD 15–20/mes (gratis 12 meses en cuenta nueva) |
| App Runner | La API como contenedor, 0.25 vCPU / 0.5 GB, 1 instancia, health check en `/health` | ~USD 5–10/mes con poco tráfico |
| Cognito | User Pool `tugu-dev` + 3 App Clients (personal, negocios, datafono) | 0 hasta 50.000 usuarios |
| ECR | Repositorio `tugu-api` para la imagen Docker | centavos |
| Secrets Manager + KMS | Credencial de RDS (generada) y clave AES de biometría (generada) | ~USD 1–2/mes |

Lo que **no** crea, a propósito: Lambda, API Gateway, RDS Proxy, Aurora, WAF,
CloudTrail/GuardDuty, ambientes Staging/Prod. Ver `context/constraints.md`.

## Pasos el día que exista la cuenta

```bash
# 0. Requisitos locales (una vez): AWS CLI, Docker, Node (para la CLI de CDK)
npm install -g aws-cdk
aws configure                      # access key, secret, región sa-east-1

# 1. Preparar la cuenta para CDK (una vez por cuenta/región)
cd infra
cdk bootstrap

# 2. Crear la infraestructura
cdk deploy TuguDev                 # ~15 min; imprime ApiUrl, UserPoolId, ClientIds, ApiImageRepositoryUri

# 3. Publicar la imagen de la API
cd ..
aws ecr get-login-password | docker login --username AWS --password-stdin <cuenta>.dkr.ecr.sa-east-1.amazonaws.com
docker build -t tugu-api .
docker tag tugu-api:latest <ApiImageRepositoryUri>:latest
docker push <ApiImageRepositoryUri>:latest    # App Runner redespliega solo

# 4. Migraciones: la API las aplica al arrancar cuando ASPNETCORE_ENVIRONMENT=Dev
#    (igual que en Development). En Prod se harán en el pipeline, no al arrancar.

# 5. Probar
curl https://<ApiUrl>/health
```

Después: copiar `ApiUrl`, `UserPoolId` y los `ClientId-*` a `docs/api-handoff.md`
(sección URLs y Autenticación) y marcar en Trello [P0][AWS], [P0][AUTH] y los
ítems de AWS de [P0][HANDOFF] / [P0][DEVOPS].

## Validar sin cuenta

```bash
cd infra
cdk synth          # genera el CloudFormation en cdk.out/ — si compila, el stack es válido
```

## Cómo lee la API los secretos

- `TUGU_DB_SECRET_JSON`: App Runner inyecta el JSON del secreto de RDS
  (`{"host","port","username","password","dbname"}`); `Program.cs` lo convierte
  en la cadena de conexión `ConnectionStrings:TuguDb`.
- `Biometrics__EncryptionKey`: valor directo del secreto (base64 de 32 bytes).
- `Cognito__*`: issuer/audience para validar JWT (ver `Tugu.Api/Auth/CognitoJwt.cs`).
