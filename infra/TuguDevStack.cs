using Amazon.CDK;
using Amazon.CDK.AWS.AppRunner;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SecretsManager;
using Constructs;

namespace Tugu.Infra;

/// <summary>
/// Infraestructura DEV mínima, decidida con el jefe (2026-09-23): RDS
/// PostgreSQL pequeño, SIN Aurora ni RDS Proxy. La API corre como contenedor en
/// App Runner (un solo servicio, escala a 1 instancia); Cognito da los JWT.
///
/// Pasos cuando exista la cuenta:
///   1. aws configure  (credenciales) · cdk bootstrap
///   2. cdk deploy TuguDev            → crea RDS, Cognito, ECR, secretos, KMS
///   3. docker build -t tugu-api . && push a la URI de ECR que imprime el stack
///   4. Configurar el App Runner con la imagen (ver salida "ApiImageRepositoryUri")
///   5. Copiar UserPoolId / UserPoolClientId a la config de la API (Cognito:*)
/// </summary>
public class TuguDevStack : Stack
{
    public TuguDevStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        // --- Red: VPC chica, la base en subred privada (nunca expuesta a internet).
        var vpc = new Vpc(this, "Vpc", new VpcProps
        {
            MaxAzs = 2,
            NatGateways = 0, // ponytail: sin NAT (cuesta ~USD 35/mes); App Runner llega a RDS vía VPC connector
            SubnetConfiguration = new[]
            {
                new SubnetConfiguration { Name = "public", SubnetType = SubnetType.PUBLIC, CidrMask = 24 },
                new SubnetConfiguration { Name = "db", SubnetType = SubnetType.PRIVATE_ISOLATED, CidrMask = 24 }
            }
        });

        // --- KMS: una llave para secretos y para la clave AES de biometría.
        var kmsKey = new Key(this, "TuguKey", new KeyProps
        {
            Alias = "alias/tugu-dev",
            EnableKeyRotation = true,
            Description = "TUGU DEV: cifrado de secretos y datos biometricos"
        });

        // --- Secretos: credencial de la base (la genera RDS) y clave AES de biometría.
        var biometricsKey = new Secret(this, "BiometricsKey", new SecretProps
        {
            SecretName = "tugu/dev/biometrics-encryption-key",
            Description = "Clave AES-256 (base64) para templates biometricos. Reemplaza a appsettings.Development.json",
            EncryptionKey = kmsKey,
            GenerateSecretString = new SecretStringGenerator
            {
                PasswordLength = 44, // 32 bytes en base64
                ExcludePunctuation = true
            }
        });

        // --- Base de datos: RDS PostgreSQL 16, instancia más pequeña, sin Multi-AZ.
        var dbSecurityGroup = new SecurityGroup(this, "DbSg", new SecurityGroupProps
        {
            Vpc = vpc, AllowAllOutbound = false, Description = "Solo la API entra a Postgres"
        });

        var db = new DatabaseInstance(this, "Postgres", new DatabaseInstanceProps
        {
            Engine = DatabaseInstanceEngine.Postgres(new PostgresInstanceEngineProps { Version = PostgresEngineVersion.VER_16 }),
            InstanceType = Amazon.CDK.AWS.EC2.InstanceType.Of(InstanceClass.BURSTABLE4_GRAVITON, InstanceSize.MICRO), // db.t4g.micro
            Vpc = vpc,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = new[] { dbSecurityGroup },
            DatabaseName = "tugu",
            Credentials = Credentials.FromGeneratedSecret("tugu", new CredentialsBaseOptions { EncryptionKey = kmsKey }),
            StorageEncryptionKey = kmsKey,
            AllocatedStorage = 20,
            MultiAz = false,
            BackupRetention = Duration.Days(7),
            DeletionProtection = false, // DEV: se puede destruir con cdk destroy
            RemovalPolicy = RemovalPolicy.SNAPSHOT
        });

        // --- Cognito: un User Pool y un App Client por app móvil (cada una con su JWT).
        var userPool = new UserPool(this, "UserPool", new UserPoolProps
        {
            UserPoolName = "tugu-dev",
            SelfSignUpEnabled = true,
            SignInAliases = new SignInAliases { Phone = true, Email = true },
            AutoVerify = new AutoVerifiedAttrs { Phone = true },
            PasswordPolicy = new PasswordPolicy { MinLength = 8, RequireDigits = true, RequireLowercase = true },
            AccountRecovery = AccountRecovery.PHONE_ONLY_WITHOUT_MFA,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        var clients = new Dictionary<string, UserPoolClient>();
        foreach (var appName in new[] { "personal", "negocios", "datafono" })
        {
            clients[appName] = userPool.AddClient($"Client-{appName}", new UserPoolClientOptions
            {
                UserPoolClientName = $"tugu-{appName}",
                AuthFlows = new AuthFlow { UserSrp = true, UserPassword = true },
                GenerateSecret = false, // apps móviles: cliente público
                AccessTokenValidity = Duration.Hours(1),
                RefreshTokenValidity = Duration.Days(30)
            });
        }

        // --- Imagen de la API.
        var repo = new Repository(this, "ApiRepo", new RepositoryProps
        {
            RepositoryName = "tugu-api",
            RemovalPolicy = RemovalPolicy.DESTROY,
            EmptyOnDelete = true
        });

        // --- App Runner: la API como contenedor, 1 instancia, con acceso a la VPC para llegar a RDS.
        var apiSecurityGroup = new SecurityGroup(this, "ApiSg", new SecurityGroupProps { Vpc = vpc, AllowAllOutbound = true });
        dbSecurityGroup.AddIngressRule(apiSecurityGroup, Port.Tcp(5432), "API -> Postgres");

        var vpcConnector = new CfnVpcConnector(this, "ApiVpcConnector", new CfnVpcConnectorProps
        {
            Subnets = vpc.SelectSubnets(new SubnetSelection { SubnetType = SubnetType.PUBLIC }).SubnetIds,
            SecurityGroups = new[] { apiSecurityGroup.SecurityGroupId }
        });

        // Rol de la instancia: solo leer sus dos secretos (mínimo privilegio).
        var instanceRole = new Role(this, "ApiInstanceRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("tasks.apprunner.amazonaws.com")
        });
        db.Secret!.GrantRead(instanceRole);
        biometricsKey.GrantRead(instanceRole);
        kmsKey.GrantDecrypt(instanceRole);

        var accessRole = new Role(this, "ApiAccessRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("build.apprunner.amazonaws.com")
        });
        repo.GrantPull(accessRole);

        var service = new CfnService(this, "ApiService", new CfnServiceProps
        {
            ServiceName = "tugu-api-dev",
            SourceConfiguration = new CfnService.SourceConfigurationProperty
            {
                AutoDeploymentsEnabled = true, // cada push a ECR :latest redespliega
                AuthenticationConfiguration = new CfnService.AuthenticationConfigurationProperty
                {
                    AccessRoleArn = accessRole.RoleArn
                },
                ImageRepository = new CfnService.ImageRepositoryProperty
                {
                    ImageRepositoryType = "ECR",
                    ImageIdentifier = $"{repo.RepositoryUri}:latest",
                    ImageConfiguration = new CfnService.ImageConfigurationProperty
                    {
                        Port = "8080",
                        RuntimeEnvironmentVariables = new[]
                        {
                            Env("ASPNETCORE_ENVIRONMENT", "Dev"),
                            Env("Cognito__Enabled", "true"),
                            Env("Cognito__Region", this.Region),
                            Env("Cognito__UserPoolId", userPool.UserPoolId),
                            Env("Cognito__ClientIds", string.Join(",", clients.Values.Select(c => c.UserPoolClientId)))
                        },
                        RuntimeEnvironmentSecrets = new[]
                        {
                            // Postgres: la conexión se arma en la API desde el JSON del secreto (host, port, username, password, dbname).
                            new CfnService.KeyValuePairProperty { Name = "TUGU_DB_SECRET_JSON", Value = db.Secret.SecretArn },
                            new CfnService.KeyValuePairProperty { Name = "Biometrics__EncryptionKey", Value = biometricsKey.SecretArn }
                        }
                    }
                }
            },
            InstanceConfiguration = new CfnService.InstanceConfigurationProperty
            {
                Cpu = "0.25 vCPU",
                Memory = "0.5 GB",
                InstanceRoleArn = instanceRole.RoleArn
            },
            NetworkConfiguration = new CfnService.NetworkConfigurationProperty
            {
                EgressConfiguration = new CfnService.EgressConfigurationProperty
                {
                    EgressType = "VPC",
                    VpcConnectorArn = vpcConnector.AttrVpcConnectorArn
                }
            },
            HealthCheckConfiguration = new CfnService.HealthCheckConfigurationProperty
            {
                Protocol = "HTTP", Path = "/health", Interval = 10, Timeout = 5, HealthyThreshold = 1, UnhealthyThreshold = 3
            }
        });

        // --- Salidas: lo que hay que copiar al handoff y a la config de la API.
        new CfnOutput(this, "ApiUrl", new CfnOutputProps { Value = $"https://{service.AttrServiceUrl}", Description = "Base URL DEV (para docs/api-handoff.md)" });
        new CfnOutput(this, "ApiImageRepositoryUri", new CfnOutputProps { Value = repo.RepositoryUri });
        new CfnOutput(this, "DbSecretArn", new CfnOutputProps { Value = db.Secret.SecretArn });
        new CfnOutput(this, "UserPoolId", new CfnOutputProps { Value = userPool.UserPoolId });
        foreach (var (name, client) in clients)
            new CfnOutput(this, $"ClientId-{name}", new CfnOutputProps { Value = client.UserPoolClientId, Description = $"App Client de TUGU {name}" });
    }

    private static CfnService.KeyValuePairProperty Env(string name, string value) =>
        new() { Name = name, Value = value };
}
