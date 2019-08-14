using System.Collections.Generic;
using System.Linq;
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
using Amazon.DynamoDBv2.Model;
using System;
using System.Globalization;
using System.IO;
using OtpNet;
using Amazon.SimpleNotificationService.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsCdkPhoneVerifyApi
{
    public class Functions
    {
        private static PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();

        private IAmazonSimpleNotificationService _sns;
        private IAmazonDynamoDB _ddb;
        private static int _maxAttempts;

        static Functions()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(formatter: new JsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();

            var maxAttempts = Environment.GetEnvironmentVariable("MAX_ATTEMPTS");
            _maxAttempts = int.TryParse(maxAttempts, out var max) ? max : 3;
        }

        public Functions()
        {
            _sns = new AmazonSimpleNotificationServiceClient();
            _ddb = new AmazonDynamoDBClient();
        }

        public Functions(IAmazonSimpleNotificationService sns, IAmazonDynamoDB ddb)
        {
            _sns = sns;
            _ddb = ddb;
        }

        public async Task<APIGatewayProxyResponse> StartAsync(APIGatewayProxyRequest request, ILambdaContext context)
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
                long? latestVersion = await GetLatestVersionAsync(startRequest.Phone);
                if (latestVersion == null)
                {
                    try
                    {
                        latestVersion = await InsertInitialVersionAsync(startRequest.Phone);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to insert initial version. Phone: {phone}", startRequest.Phone);

                        // If two threads at the same time tried to insert the initial version only 1 request will succeed.
                        // So we attempt to lookup the latest version..
                        latestVersion = await GetLatestVersionAsync(startRequest.Phone);
                    }
                }

                Verification current = await GetVerificationAsync(startRequest.Phone, latestVersion.Value);
                Log.Information("Current: {@current}", current);

                // Check that it hasn't expired
                if (current.Verified.HasValue || current.Expired)
                {
                    Log.Information("Inserting next verification. Phone: {phone}", current.Phone, current.Version);
                    current = await InsertNextVersionAsync(current.Phone, current.Version);
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

                    var publishResponse = await _sns.PublishAsync(publishRequest);
                    Log.Information("Published message. MessageId: {messageId}", publishResponse.MessageId);
                }

                var json = JsonConvert.SerializeObject(new StartResponse { Id = current.Id }, Formatting.None);
                return new APIGatewayProxyResponse { StatusCode = 200, Body = json };
            }
        }

        public async Task<APIGatewayProxyResponse> CheckAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (LogContext.PushProperty("AwsRequestId", context.AwsRequestId))
            {
                var checkRequest = JsonConvert.DeserializeObject<CheckRequest>(request.Body);
                Log.Information("CheckRequest. Id: {id}", checkRequest.Id);

                var verification = await GetVerificationAsync(checkRequest.Id);
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
                    await IncrementAttemptsAsync(verification.Phone, verification.Version);
                    return ErrorResponse(400, "Invalid code");
                }

                await SetVerifiedAsync(verification.Phone, verification.Version);

                var json = JsonConvert.SerializeObject(new CheckResponse { Verified = true }, Formatting.None);
                return new APIGatewayProxyResponse { StatusCode = 200, Body = json };
            }
        }

        private async Task IncrementAttemptsAsync(string phone, long version)
        {
            var request = new UpdateItemRequest
            {
                TableName = "Verifications",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Phone"] = new AttributeValue { S = phone },
                    ["Version"] = new AttributeValue { N = version.ToString() },
                },
                UpdateExpression = "SET Attempts = Attempts + :Value",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    [":Value"] = new AttributeValue() { N = 1.ToString() }
                }
            };

            await _ddb.UpdateItemAsync(request);
        }

        private async Task SetVerifiedAsync(string phone, long version)
        {
            var request = new UpdateItemRequest
            {
                TableName = "Verifications",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Phone"] = new AttributeValue { S = phone },
                    ["Version"] = new AttributeValue { N = version.ToString() },
                },
                UpdateExpression = "SET Verified = :Verified, Attempts = Attempts + :Value",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    [":Verified"] = new AttributeValue() { S = DateTime.UtcNow.ToString("o") },
                    [":Value"] = new AttributeValue() { N = 1.ToString() }
                }
            };

            await _ddb.UpdateItemAsync(request);
        }

        private async Task<Verification> InsertNextVersionAsync(string phone, long currentVersion)
        {
            byte[] secretKey = KeyGeneration.GenerateRandomKey(20);
            var nextVersion = currentVersion + 1;
            var utcNow = DateTime.UtcNow;
            var id = Guid.NewGuid();

            var request = new TransactWriteItemsRequest
            {
                TransactItems = new List<TransactWriteItem>
                {
                    new TransactWriteItem
                    {
                        Update = new Update
                        {
                            TableName = "Verifications",
                            Key = new Dictionary<string, AttributeValue>()
                            {
                                ["Phone"] = new AttributeValue { S = phone },
                                ["Version"] = new AttributeValue { N = 0.ToString() },
                            },
                            ConditionExpression = "Latest = :CurrentVersion",
                            UpdateExpression = "SET Latest = :NextVersion",
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                            {
                                [":CurrentVersion"] = new AttributeValue() { N = currentVersion.ToString() },
                                [":NextVersion"] = new AttributeValue() { N = nextVersion.ToString() }
                            }
                        }
                    },
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = "Verifications",
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["Phone"] = new AttributeValue { S = phone },
                                ["Version"] = new AttributeValue { N = nextVersion.ToString() },
                                ["Id"] = new AttributeValue { S = id.ToString() },
                                ["Created"] = new AttributeValue { S = utcNow.ToString("o") },
                                ["Attempts"]= new AttributeValue { N = 0.ToString() },
                                ["SecretKey"] = new AttributeValue { B = new MemoryStream(secretKey) }
                            }
                        },
                    }
                }
            };

            var response = await _ddb.TransactWriteItemsAsync(request);

            var verification = new Verification
            {
                Phone = phone,
                SecretKey = secretKey,
                Version = nextVersion,
                Attempts = 0,
                Verified = null,
                Created = utcNow,
                Id = id,
            };

            return verification;
        }

        private async Task<long?> InsertInitialVersionAsync(string phone)
        {
            byte[] secretKey = KeyGeneration.GenerateRandomKey(20);

            var request = new TransactWriteItemsRequest
            {
                TransactItems = new List<TransactWriteItem>
                {
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = "Verifications",
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["Phone"] = new AttributeValue { S = phone },
                                ["Version"] = new AttributeValue { N = 0.ToString() },
                                ["Latest"] = new AttributeValue { N = 1.ToString() },
                            },
                        }
                    },
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = "Verifications",
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["Phone"] = new AttributeValue { S = phone },
                                ["Version"] = new AttributeValue { N = 1.ToString() },
                                ["Id"] = new AttributeValue { S = Guid.NewGuid().ToString() },
                                ["Created"] = new AttributeValue { S = DateTime.UtcNow.ToString("o") },
                                ["Attempts"]= new AttributeValue { N = 0.ToString() },
                                ["SecretKey"] = new AttributeValue { B = new MemoryStream(secretKey) }
                            }
                        },
                    }
                }
            };

            var response = await _ddb.TransactWriteItemsAsync(request);
            return 1;
        }

        public async Task<Verification> GetVerificationAsync(Guid id)
        {
            var request = new QueryRequest
            {
                TableName = "Verifications",
                IndexName = "IdIndex",
                ConsistentRead = false,
                KeyConditionExpression = "Id = :Id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":Id"] = new AttributeValue() { S = id.ToString() }
                }
            };

            var response = await _ddb.QueryAsync(request);
            if (!response.Items.Any())
            {
                return null;
            }

            var item = response.Items.SingleOrDefault();
            var verification = Map(item);
            return verification;
        }

        private Verification Map(Dictionary<string, AttributeValue> item)
        {
            var verification = new Verification
            {
                Phone = item["Phone"].S,
                Attempts = int.Parse(item["Attempts"].N),
                Id = Guid.Parse(item["Id"].S),
                Created = DateTime.Parse(item["Created"].S, null, DateTimeStyles.RoundtripKind),
                Version = long.Parse(item["Version"].N),
                SecretKey = item["SecretKey"].B.ToArray()
            };

            if (item.ContainsKey("Verified"))
            {
                verification.Verified = DateTime.Parse(item["Verified"].S);
            }

            return verification;
        }

        public async Task<Verification> GetVerificationAsync(string phone, long version)
        {
            var request = new QueryRequest
            {
                TableName = "Verifications",
                ConsistentRead = true,
                KeyConditionExpression = "Phone = :Phone AND Version = :Version",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    [":Phone"] = new AttributeValue() { S = phone },
                    [":Version"] = new AttributeValue() { N = version.ToString() }
                }
            };

            var response = await _ddb.QueryAsync(request);
            if (!response.Items.Any())
            {
                return null;
            }

            var item = response.Items.SingleOrDefault();
            var verification = Map(item);
            return verification;
        }

        private async Task<long?> GetLatestVersionAsync(string phone)
        {
            var request = new QueryRequest
            {
                TableName = "Verifications",
                ConsistentRead = true,
                KeyConditionExpression = "Phone = :Phone AND Version = :Version",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    [":Phone"] = new AttributeValue() { S = phone },
                    [":Version"] = new AttributeValue() { N = 0.ToString() }
                }
            };

            var response = await _ddb.QueryAsync(request);
            if (!response.Items.Any())
            {
                return null;
            }
            var verification = response.Items.SingleOrDefault();
            var latest = long.Parse(verification["Latest"].N);
            return latest;
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
