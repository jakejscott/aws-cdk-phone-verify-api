using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using PhoneNumbers;
using Serilog;
using Serilog.Context;
using System;
using OtpNet;
using Amazon.SimpleNotificationService.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsCdkPhoneVerifyApi.StartLambda
{
    public class Function
    {
        private static readonly PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();

        private readonly IAmazonSimpleNotificationService sns;
        private readonly IVerificationsRepository repo;

        static Function()
        {
            Log.Logger = SerilogLogging.ConfigureLogging();
        }

        public Function()
        {
            sns = new AmazonSimpleNotificationServiceClient();
            repo = new VerificationsRepository(new AmazonDynamoDBClient());
        }

        public Function(IAmazonSimpleNotificationService sns, IVerificationsRepository repo)
        {
            this.sns = sns;
            this.repo = repo;
        }

        public async Task<APIGatewayProxyResponse> ExecuteAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (LogContext.PushProperty("AwsRequestId", context.AwsRequestId))
            {
                var startRequest = JsonConvert.DeserializeObject<StartRequest>(request.Body);
                Log.Information("StartRequest. Phone: {phone}", startRequest.Phone);

                if (string.IsNullOrWhiteSpace(startRequest.Phone))
                {
                    return ErrorResponse(400, "Phone required");
                }

                try
                {
                    var phoneNumber = phoneNumberUtil.Parse(startRequest.Phone, null);
                    startRequest.Phone = phoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.E164);
                }
                catch
                {
                    Log.Warning("Invalid phone: {phone}", startRequest.Phone);
                    return ErrorResponse(400, "Phone invalid");
                }

                // Lookup the "Latest" verification for this phone number.
                long? latestVersion = await repo.GetLatestVersionAsync(startRequest.Phone);
                if (latestVersion == null)
                {
                    try
                    {
                        latestVersion = await repo.InsertInitialVersionAsync(startRequest.Phone);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to insert initial version. Phone: {phone}", startRequest.Phone);

                        // If two threads at the same time tried to insert the initial version only 1 request will succeed.
                        // So we attempt to lookup the latest version..
                        latestVersion = await repo.GetLatestVersionAsync(startRequest.Phone);
                    }
                }

                Verification current = await repo.GetVerificationAsync(startRequest.Phone, latestVersion.Value);
                Log.Information("Current: {@current}", current);

                // Check that it hasn't expired
                if (current.Verified.HasValue || current.Expired)
                {
                    Log.Information("Inserting next verification. Phone: {phone}", current.Phone, current.Version);
                    current = await repo.InsertNextVersionAsync(current.Phone, current.Version);
                }

                var hotp = new Hotp(current.SecretKey);
                var code = hotp.ComputeHOTP(current.Version);
                {
                    var message = $"Your code is: {code}";
                    Log.Information("Sending message: {message}", message);

                    var publishRequest = new PublishRequest
                    {
                        PhoneNumber = startRequest.Phone,
                        Message = message
                    };

                    var publishResponse = await sns.PublishAsync(publishRequest);
                    Log.Information("Published message. MessageId: {messageId}", publishResponse.MessageId);
                }

                var json = JsonConvert.SerializeObject(new StartResponse { Id = current.Id }, Formatting.None);
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
