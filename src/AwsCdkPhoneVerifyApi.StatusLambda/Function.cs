using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Serilog;
using Serilog.Context;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsCdkPhoneVerifyApi.StatusLambda
{
    public class Function
    {
        private readonly IVerificationsRepository _repo;

        static Function()
        {
            Log.Logger = SerilogLogging.ConfigureLogging();
        }

        public Function()
        {
            _repo = new VerificationsRepository(new AmazonDynamoDBClient());
        }

        public Function(IVerificationsRepository repo)
        {
            _repo = repo;
        }

        public async Task<APIGatewayProxyResponse> ExecuteAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (LogContext.PushProperty("AwsRequestId", context.AwsRequestId))
            {
                var statusRequest = JsonConvert.DeserializeObject<StatusRequest>(request.Body);
                Log.Information("StatusRequest. Id: {id}", statusRequest.Id);

                var verification = await _repo.GetVerificationAsync(statusRequest.Id);
                if (verification == null)
                {
                    return ErrorResponse(400, "Not found");
                }

                Log.Information("Verification. Id: {id}", verification.Id);

                var json = JsonConvert.SerializeObject(new StatusResponse
                {
                    Verified = verification.Verified,
                    Phone = verification.Phone,
                    Created = verification.Created,
                    Id = verification.Id
                }, Formatting.None);

                return new APIGatewayProxyResponse { StatusCode = 200, Body = json };
            }
        }

        private APIGatewayProxyResponse ErrorResponse(int statusCode, string error)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = statusCode,
                Body = JsonConvert.SerializeObject(new ErrorResponse { Error = error })
            };
        }
    }
}
