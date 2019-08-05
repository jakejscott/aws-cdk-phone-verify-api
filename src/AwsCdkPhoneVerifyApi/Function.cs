using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using PhoneNumbers;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsCdkPhoneVerifyApi
{
    public class Functions
    {
        private static PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();

        private ILogger _logger;
        private IAmazonSimpleNotificationService _sns;

        public Functions() 
        {
            _logger = new LoggerConfiguration()
                .WriteTo.Console(formatter: new JsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();

            _sns = new AmazonSimpleNotificationServiceClient();
        }

        public Functions(IAmazonSimpleNotificationService sns)
        {
            _logger = new LoggerConfiguration()
                .WriteTo.Console(formatter: new JsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();

            _sns = sns;
        }

        public async Task<APIGatewayProxyResponse> StartAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (LogContext.PushProperty("AwsRequestId", context.AwsRequestId))
            {
                await Task.Delay(0);

                var startRequest = JsonConvert.DeserializeObject<StartRequest>(request.Body);
                _logger.Information("StartRequest. Phone: {phone}", startRequest.Phone);

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
                    _logger.Warning("Invalid phone: {phone}", startRequest.Phone);
                    return ErrorResponse(400, "Phone invalid");
                }

                {
                    var publishRequest = new PublishRequest
                    {
                        PhoneNumber = startRequest.Phone,
                        Message = "Your code is: 123456"
                    };

                    var publishResponse = await _sns.PublishAsync(publishRequest);
                    _logger.Information("Published message. MessageId: {messageId}", publishResponse.MessageId);

                }

                var json = JsonConvert.SerializeObject(startRequest, Formatting.None);
                Console.WriteLine($"Request: {json}");

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
