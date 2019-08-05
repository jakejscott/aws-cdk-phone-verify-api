import cdk = require('@aws-cdk/core');
import lambda = require('@aws-cdk/aws-lambda');
import apigateway = require('@aws-cdk/aws-apigateway');
import { PolicyStatement, Effect } from '@aws-cdk/aws-iam';

export class AwsCdkPhoneVerifyApiStack extends cdk.Stack {
  constructor(scope: cdk.Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const start = new lambda.Function(this, 'StartLambda', {
      code: lambda.Code.asset('src/AwsCdkPhoneVerifyApi/bin/Debug/netcoreapp2.1/publish'),
      runtime: lambda.Runtime.DOTNET_CORE_2_1,
      handler: 'AwsCdkPhoneVerifyApi::AwsCdkPhoneVerifyApi.Functions::StartAsync',
      memorySize: 3008,
      timeout: cdk.Duration.seconds(30),
    });

    var snsPolicy = new PolicyStatement({
      actions: [ "sns:*" ],
      resources: ["*"],
      effect: Effect.ALLOW
    });

    start.addToRolePolicy(snsPolicy);

    const api = new apigateway.RestApi(this, 'AwsCdkPhoneVerifyApi', {
      restApiName: 'AwsCdkPhoneVerifyApi'
    });

    const verifyRoute = api.root.addResource('verify');

    const startRoute = verifyRoute.addResource('start');
    startRoute.addMethod('POST', new apigateway.LambdaIntegration(start));
  }
}
