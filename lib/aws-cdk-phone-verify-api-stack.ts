import cdk = require('@aws-cdk/core');
import lambda = require('@aws-cdk/aws-lambda');
import apigateway = require('@aws-cdk/aws-apigateway');
import dynamodb = require('@aws-cdk/aws-dynamodb');
import { PolicyStatement, Effect } from '@aws-cdk/aws-iam';
import { BillingMode } from '@aws-cdk/aws-dynamodb';

export class AwsCdkPhoneVerifyApiStack extends cdk.Stack {
  constructor(scope: cdk.Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const table = new dynamodb.Table(this, 'VerificationsTable', {
      tableName: 'Verifications',
      billingMode: BillingMode.PAY_PER_REQUEST,
      partitionKey: {
        name: 'Phone',
        type: dynamodb.AttributeType.STRING
      },
      sortKey: {
        name: 'Version',
        type: dynamodb.AttributeType.NUMBER
      }
    });

    table.addGlobalSecondaryIndex({
      indexName: 'IdIndex',
      partitionKey: { 
        name: 'Id',
        type: dynamodb.AttributeType.STRING
       },
       projectionType: dynamodb.ProjectionType.ALL
    });

    const environment = {
      'MAX_ATTEMPTS': '3'
    };

    const start = new lambda.Function(this, 'StartLambda', {
      code: lambda.Code.asset('src/AwsCdkPhoneVerifyApi.StartLambda/bin/Debug/netcoreapp2.1/publish'),
      runtime: lambda.Runtime.DOTNET_CORE_2_1,
      handler: 'AwsCdkPhoneVerifyApi.StartLambda::AwsCdkPhoneVerifyApi.StartLambda.Function::ExecuteAsync',
      memorySize: 3008,
      timeout: cdk.Duration.seconds(30),
      environment: environment
    });

    const check = new lambda.Function(this, 'CheckLambda', {
      code: lambda.Code.asset('src/AwsCdkPhoneVerifyApi.CheckLambda/bin/Debug/netcoreapp2.1/publish'),
      runtime: lambda.Runtime.DOTNET_CORE_2_1,
      handler: 'AwsCdkPhoneVerifyApi.CheckLambda::AwsCdkPhoneVerifyApi.CheckLambda.Function::ExecuteAsync',
      memorySize: 3008,
      timeout: cdk.Duration.seconds(30),
      environment: environment
    });

    const status = new lambda.Function(this, 'StatusLambda', {
      code: lambda.Code.asset('src/AwsCdkPhoneVerifyApi.StatusLambda/bin/Debug/netcoreapp2.1/publish'),
      runtime: lambda.Runtime.DOTNET_CORE_2_1,
      handler: 'AwsCdkPhoneVerifyApi.StatusLambda::AwsCdkPhoneVerifyApi.StatusLambda.Function::ExecuteAsync',
      memorySize: 3008,
      timeout: cdk.Duration.seconds(60),
      environment: environment
    });

    var snsPolicy = new PolicyStatement({
      actions: [ "sns:*" ],
      resources: ["*"],
      effect: Effect.ALLOW
    });

    start.addToRolePolicy(snsPolicy);
    table.grantReadWriteData(start); 
    table.grantReadWriteData(check); 
    table.grantReadWriteData(status);

    const api = new apigateway.RestApi(this, 'AwsCdkPhoneVerifyApi', {
      restApiName: 'AwsCdkPhoneVerifyApi'
    });

    const verifyRoute = api.root.addResource('verify');

    const startRoute = verifyRoute.addResource('start');
    const startMethod = startRoute.addMethod('POST', new apigateway.LambdaIntegration(start), { apiKeyRequired: true });

    const checkRoute = verifyRoute.addResource('check');
    const checkMethod = checkRoute.addMethod('POST', new apigateway.LambdaIntegration(check), { apiKeyRequired: true });

    const statusRoute = verifyRoute.addResource('status');
    const statusMethod = statusRoute.addMethod('POST', new apigateway.LambdaIntegration(status), { apiKeyRequired: true });

    const key = api.addApiKey('ApiKey');

    const plan = api.addUsagePlan('UsagePlan', {
      apiKey: key,
      name: 'Basic',
      throttle: {
        burstLimit: 2,
        rateLimit: 10
      }
    });

    plan.addApiStage({
      stage: api.deploymentStage,
      throttle: [
        {
          method: startMethod,
          throttle: { rateLimit: 10, burstLimit: 2 }
        },
        {
          method: checkMethod,
          throttle: { rateLimit: 10, burstLimit: 2 }
        },
        {
          method: statusMethod,
          throttle: { rateLimit: 10, burstLimit: 2 }
        }
      ]
    });

  }
}
