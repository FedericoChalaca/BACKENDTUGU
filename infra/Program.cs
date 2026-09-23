using Amazon.CDK;
using Tugu.Infra;

// Un solo ambiente por ahora (DEV). Con credenciales configuradas, `cdk deploy`
// llena CDK_DEFAULT_ACCOUNT/REGION solo; sin ellas (`cdk synth` local) el stack
// queda agnóstico de cuenta y no necesita llamar a AWS.
var app = new App();

var account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT");
var region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION") ?? "sa-east-1";

new TuguDevStack(app, "TuguDev", new StackProps
{
    Env = account is null ? null : new Amazon.CDK.Environment { Account = account, Region = region },
    Description = "TUGU backend - ambiente DEV (prototipo): RDS PostgreSQL pequeño + API en App Runner + Cognito"
});

app.Synth();
