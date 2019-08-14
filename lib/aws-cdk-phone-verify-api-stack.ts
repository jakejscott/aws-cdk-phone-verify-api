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
      code: lambda.Code.asset('src/AwsCdkPhoneVerifyApi/bin/Debug/netcoreapp2.1/publish'),
      runtime: lambda.Runtime.DOTNET_CORE_2_1,
      handler: 'AwsCdkPhoneVerifyApi::AwsCdkPhoneVerifyApi.Functions::StartAsync',
      memorySize: 3008,
      timeout: cdk.Duration.seconds(30),
      environment: environment
    });

    const check = new lambda.Function(this, 'CheckLambda', {
      code: lambda.Code.asset('src/AwsCdkPhoneVerifyApi/bin/Debug/netcoreapp2.1/publish'),
      runtime: lambda.Runtime.DOTNET_CORE_2_1,
      handler: 'AwsCdkPhoneVerifyApi::AwsCdkPhoneVerifyApi.Functions::CheckAsync',
      memorySize: 3008,
      timeout: cdk.Duration.seconds(30),
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

    const api = new apigateway.RestApi(this, 'AwsCdkPhoneVerifyApi', {
      restApiName: 'AwsCdkPhoneVerifyApi'
    });

    const verifyRoute = api.root.addResource('verify');

    const startRoute = verifyRoute.addResource('start');
    startRoute.addMethod('POST', new apigateway.LambdaIntegration(start));

    const checkRoute = verifyRoute.addResource('check');
    checkRoute.addMethod('POST', new apigateway.LambdaIntegration(check));
  }
}
