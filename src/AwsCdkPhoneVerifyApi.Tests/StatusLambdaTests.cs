using System;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using AwsCdkPhoneVerifyApi.StatusLambda;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace AwsCdkPhoneVerifyApi.Tests
{
    [TestFixture]
    public class StatusLambdaTests : BaseLambdaTest
    {
        private Function function;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            function = new Function(repo);
        }

        [Test]
        public async Task VerificationNotFound()
        {
            // Arrange
            var startRequest = new StatusRequest { Id = Guid.NewGuid() };
            var request = CreateRequest(startRequest);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(400, response.StatusCode);
            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Not found", error.Error);
        }

        [Test]
        public async Task Valid()
        {
            var id = Guid.NewGuid();
            var secret = Encoding.UTF8.GetBytes("secret");

            // Arrange
            var startRequest = new StatusRequest { Id = id };
            var request = CreateRequest(startRequest);

            // Mock
            var verification = new Verification
            {
                Id = id,
                Phone = phone,
                Created = DateTime.UtcNow,
                Attempts = 3,
                SecretKey = secret,
                Version = 1,
                Verified = DateTime.UtcNow
            };
            repo.GetVerificationAsync(id).Returns(verification);
        
            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());
        
            // Assert
            Assert.AreEqual(200, response.StatusCode);
        
            var statusResponse = JsonConvert.DeserializeObject<StatusResponse>(response.Body);

            Assert.AreEqual(verification.Id, statusResponse.Id);
            Assert.AreEqual(verification.Verified, statusResponse.Verified);
            Assert.AreEqual(verification.Phone, statusResponse.Phone);
            Assert.AreEqual(verification.Created, statusResponse.Created);
        }
    }
}