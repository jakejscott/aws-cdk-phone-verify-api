using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Serilog;
using Serilog.Context;
using System;
using OtpNet;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsCdkPhoneVerifyApi.CheckLambda
{
    public class Function
    {
        private readonly IVerificationsRepository _repo;
        private static readonly int _maxAttempts;

        static Function()
        {
            Log.Logger = SerilogLogging.ConfigureLogging();

            var maxAttempts = Environment.GetEnvironmentVariable("MAX_ATTEMPTS");
            _maxAttempts = int.TryParse(maxAttempts, out var max) ? max : 3;
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
                var checkRequest = JsonConvert.DeserializeObject<CheckRequest>(request.Body);
                Log.Information("CheckRequest. Id: {id}", checkRequest.Id);

                var verification = await _repo.GetVerificationAsync(checkRequest.Id);
                if (verification == null)
                {
                    return ErrorResponse(400, "Not found");
                }

                if (verification.Verified.HasValue)
                {
                    return ErrorResponse(400, "Already verified");
                }

                if (verification.Expired)
                {
                    return ErrorResponse(400, "Expired");
                }

                if (verification.Attempts > _maxAttempts)
                {
                    return ErrorResponse(400, "Attempts exceeded");
                }

                var hotp = new Hotp(verification.SecretKey);
                if (!hotp.VerifyHotp(checkRequest.Code, verification.Version))
                {
                    await _repo.IncrementAttemptsAsync(verification.Phone, verification.Version);
                    return ErrorResponse(400, "Invalid code");
                }

                await _repo.SetVerifiedAsync(verification.Phone, verification.Version);

                var json = JsonConvert.SerializeObject(new CheckResponse { Verified = true }, Formatting.None);
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
