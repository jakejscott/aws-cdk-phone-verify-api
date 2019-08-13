using AwsCdkPhoneVerifyApi;
using NUnit.Framework;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.SimpleNotificationService;
using Newtonsoft.Json;
using OtpNet;
using System;

namespace Tests
{
    [TestFixture]
    public class FunctionsTests
    {
        private static RegionEndpoint DefaultRegion = RegionEndpoint.APSoutheast2;
        private Functions functions;

        [SetUp]
        public void SetUp()
        {
            var sns = new AmazonSimpleNotificationServiceClient(DefaultRegion);
            var ddb = new AmazonDynamoDBClient(DefaultRegion);
            
            functions = new Functions(sns, ddb);
        }

        [Test]
        public async Task PhoneIsRequired()
        {
            var startRequest = new StartRequest { Phone = null };

            var request = new APIGatewayProxyRequest()
            {
                Body = JsonConvert.SerializeObject(startRequest)
            };

            var context = new TestLambdaContext();
            var response = await functions.StartAsync(request, context);

            Assert.AreEqual(400, response.StatusCode);

            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Phone required", error.Error);
        }

        [Test]
        public async Task PhoneIsInvalid()
        {
            var startRequest = new StartRequest { Phone = "abc1234" };
            var request = new APIGatewayProxyRequest()
            {
                Body = JsonConvert.SerializeObject(startRequest)
            };

            var context = new TestLambdaContext();
            var response = await functions.StartAsync(request, context);

            Assert.AreEqual(400, response.StatusCode);

            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Phone invalid", error.Error);
        }

        [Test]
        public async Task PhoneIsValid()
        {
            StartResponse startResponse;
            {
                var startRequest = new StartRequest { Phone = "+64223062141" };
                var request = new APIGatewayProxyRequest() { Body = JsonConvert.SerializeObject(startRequest) };
                var context = new TestLambdaContext();
                var response = await functions.StartAsync(request, context);

                Assert.AreEqual(200, response.StatusCode);
                startResponse = JsonConvert.DeserializeObject<StartResponse>(response.Body);
                Assert.NotNull(startResponse.Id);
            }

            {
                var checkRequest = new CheckRequest { Id = startResponse.Id.Value };

                var request = new APIGatewayProxyRequest() { Body = JsonConvert.SerializeObject(checkRequest) };
                var context = new TestLambdaContext();

                var response = await functions.CheckAsync(request, context);
                Assert.AreEqual(200, response.StatusCode);

                var checkResponse = JsonConvert.DeserializeObject<CheckResponse>(response.Body);
                Assert.True(checkResponse.Verified);
            }
        }
    }
}