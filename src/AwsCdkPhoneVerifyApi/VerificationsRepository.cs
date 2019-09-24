using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Core;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System;
using System.Globalization;
using System.IO;
using OtpNet;

namespace AwsCdkPhoneVerifyApi
{
    public class VerificationsRepository : IVerificationsRepository
    {
        private IAmazonDynamoDB _ddb;

        public VerificationsRepository(IAmazonDynamoDB ddb)
        {
            _ddb = ddb ?? throw new ArgumentNullException(nameof(ddb));
        }

        public async Task IncrementAttemptsAsync(string phone, long version)
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

        public async Task SetVerifiedAsync(string phone, long version)
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

        public async Task<List<Verification>> GetLatestVerificationsAsync(string phone, int limit)
        {
            var request = new QueryRequest
            {
                TableName = "Verifications",
                ConsistentRead = true,
                ScanIndexForward = false,
                Limit = limit,
                KeyConditionExpression = "Phone = :Phone AND Version > :Version",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":Phone"] = new AttributeValue { S = phone },
                    [":Version"] = new AttributeValue { N = 0.ToString() }
                }
            };

            var response = await _ddb.QueryAsync(request);
            var verifications = response.Items.Select(Map).ToList();
            return verifications;
        }

        public bool HasExceeededRateLimit(List<Verification> verifications, int limit, DateTimeOffset min)
        {
            var count = verifications.Count(x => x.Created >= min);
            return count >= limit;
        }

        public async Task<Verification> InsertNextVersionAsync(string phone, long currentVersion)
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

            await _ddb.TransactWriteItemsAsync(request);

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

        public async Task<long?> InsertInitialVersionAsync(string phone)
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

            await _ddb.TransactWriteItemsAsync(request);
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

        public async Task<long?> GetLatestVersionAsync(string phone)
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
    }
}
