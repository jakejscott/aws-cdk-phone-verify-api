using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using PhoneNumbers;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Json;
using System;
using OtpNet;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsCdkPhoneVerifyApi.StatusLambda
{
    public class Function
    {
        private static PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();

        private IAmazonSimpleNotificationService _sns;
        private IVerificationsRepository _repo;
        private static int _maxAttempts;

        static Function()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(formatter: new JsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();

            var maxAttempts = Environment.GetEnvironmentVariable("MAX_ATTEMPTS");
            _maxAttempts = int.TryParse(maxAttempts, out var max) ? max : 3;
        }

        public Function()
        {
            _sns = new AmazonSimpleNotificationServiceClient();
            _repo = new VerificationsRepository(new AmazonDynamoDBClient());
        }

        public Function(IAmazonSimpleNotificationService sns, IVerificationsRepository repo)
        {
            _sns = sns;
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
