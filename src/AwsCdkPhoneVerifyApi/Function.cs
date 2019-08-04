using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System;
using Newtonsoft.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsCdkPhoneVerifyApi
{
    public class Functions
    {
        public APIGatewayProxyResponse StartAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var startRequest = JsonConvert.DeserializeObject<StartRequest>(request.Body);

            var json = JsonConvert.SerializeObject(startRequest, Formatting.None);
            Console.WriteLine($"Request: {json}");

            return new APIGatewayProxyResponse { StatusCode = 200, Body = json };
        }
    }
}
