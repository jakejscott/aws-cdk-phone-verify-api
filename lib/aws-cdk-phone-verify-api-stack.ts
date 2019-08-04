import cdk = require('@aws-cdk/core');
import lambda = require('@aws-cdk/aws-lambda');
import apigateway = require('@aws-cdk/aws-apigateway');

export class AwsCdkPhoneVerifyApiStack extends cdk.Stack {
  constructor(scope: cdk.Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const start = new lambda.Function(this, 'HelloWorld', {
      code: lambda.Code.asset('src/AwsCdkPhoneVerifyApi/bin/Debug/netcoreapp2.1/publish'),
      runtime: lambda.Runtime.DOTNET_CORE_2_1,
      handler: 'AwsCdkPhoneVerifyApi::AwsCdkPhoneVerifyApi.Functions::StartAsync',
      memorySize: 3008,
    });

    const api = new apigateway.RestApi(this, 'AwsCdkPhoneVerifyApi', {
      restApiName: 'AwsCdkPhoneVerifyApi'
    });

    const verifyRoute = api.root.addResource('verify');

    const startRoute = verifyRoute.addResource('start');
    startRoute.addMethod('POST', new apigateway.LambdaIntegration(start));
  }
}
