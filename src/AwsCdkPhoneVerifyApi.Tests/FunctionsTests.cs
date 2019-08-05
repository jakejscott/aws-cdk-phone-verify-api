using AwsCdkPhoneVerifyApi;
using NUnit.Framework;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.SimpleNotificationService;
using Newtonsoft.Json;

namespace Tests
{
    [TestFixture]
    public class FunctionsTests
    {
        [Test]
        public async Task PhoneIsRequired()
        {
            var startRequest = new StartRequest { Phone = null };

            var request = new APIGatewayProxyRequest()
            {
                Body = JsonConvert.SerializeObject(startRequest)
            };

            var context = new TestLambdaContext();
            var functions = new Functions();
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
            var functions = new Functions();
            var response = await functions.StartAsync(request, context);

            Assert.AreEqual(400, response.StatusCode);

            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Phone invalid", error.Error);
        }

        [Test]
        public async Task PhoneIsValid()
        {
            var startRequest = new StartRequest { Phone = "+64223062141" };

            var request = new APIGatewayProxyRequest()
            {
                Body = JsonConvert.SerializeObject(startRequest)
            };

            var context = new TestLambdaContext();

            var sns = new AmazonSimpleNotificationServiceClient(RegionEndpoint.APSoutheast2);
            var functions = new Functions(sns: sns);

            var response = await functions.StartAsync(request, context);

            Assert.AreEqual(200, response.StatusCode);
        }
    }
}