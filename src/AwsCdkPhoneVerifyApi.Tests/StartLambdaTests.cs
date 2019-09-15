using System;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AwsCdkPhoneVerifyApi.StartLambda;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using OtpNet;

namespace AwsCdkPhoneVerifyApi.Tests
{
    [TestFixture]
    public class StartLambdaTests
    {
        const string phone = "+64223062141";

        private Function function;

        private IAmazonSimpleNotificationService sns;
        private IVerificationsRepository repo;

        [SetUp]
        public void SetUp()
        {
            sns = Substitute.For<IAmazonSimpleNotificationService>();
            repo = Substitute.For<IVerificationsRepository>();

            function = new Function(sns, repo);
        }

        private APIGatewayProxyRequest CreateRequest<T>(T value) => new APIGatewayProxyRequest { Body = JsonConvert.SerializeObject(value) };

        [Test]
        public async Task PhoneIsRequired()
        {
            // Arrange
            var startRequest = new StartRequest { Phone = null };
            var request = CreateRequest(startRequest);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(400, response.StatusCode);
            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Phone required", error.Error);
        }

        [Test]
        public async Task PhoneIsInvalid()
        {
            // Arrange
            var startRequest = new StartRequest { Phone = "abc1234" };
            var request = CreateRequest(startRequest);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(400, response.StatusCode);
            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Phone invalid", error.Error);
        }

        [Test]
        public async Task VerificationIsExpired()
        {
            // Arrange
            var startRequest = new StartRequest { Phone = phone };
            var request = CreateRequest(startRequest);

            // Mock
            repo.GetLatestVersionAsync(phone).Returns(1);

            // Mock
            var current = new Verification
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Version = 1,
                Verified = null,
                Attempts = 0,
                Created = DateTime.UtcNow.AddMinutes(-4),
                SecretKey = Encoding.UTF8.GetBytes("secret")
            };
            repo.GetVerificationAsync(phone, 1).Returns(current);

            // Mock
            var nextVersion = new Verification
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Version = 2,
                Verified = null,
                Attempts = 0,
                Created = DateTime.UtcNow,
                SecretKey = Encoding.UTF8.GetBytes("secret")
            };
            repo.InsertNextVersionAsync(phone, 1).Returns(nextVersion);

            // Mock
            sns.PublishAsync(Arg.Any<PublishRequest>()).Returns(new PublishResponse { MessageId = "test1" });

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(200, response.StatusCode);
            var startResponse = JsonConvert.DeserializeObject<StartResponse>(response.Body);
            Assert.AreEqual(nextVersion.Id, startResponse.Id);

            var hotp = new Hotp(current.SecretKey);
            var code = hotp.ComputeHOTP(nextVersion.Version);
            await sns.Received(1).PublishAsync(Arg.Is<PublishRequest>(x => x.PhoneNumber == phone && x.Message == $"Your code is: {code}"));

        }

        [Test]
        public async Task VerificationIsNotExpired()
        {
            // Arrange
            var startRequest = new StartRequest { Phone = phone };
            var request = CreateRequest(startRequest);

            // Mock
            repo.GetLatestVersionAsync(phone).Returns(1);

            // Mock
            var current = new Verification
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Version = 1,
                Verified = null,
                Attempts = 0,
                Created = DateTime.UtcNow,
                SecretKey = Encoding.UTF8.GetBytes("secret")
            };
            repo.GetVerificationAsync(phone, 1).Returns(current);

            // Mock
            sns.PublishAsync(Arg.Any<PublishRequest>()).Returns(new PublishResponse { MessageId = "test1" });

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(200, response.StatusCode);
            await repo.DidNotReceive().InsertNextVersionAsync(phone, 1);

            var startResponse = JsonConvert.DeserializeObject<StartResponse>(response.Body);
            Assert.AreEqual(current.Id, startResponse.Id);

            var hotp = new Hotp(current.SecretKey);
            var code = hotp.ComputeHOTP(current.Version);
            await sns.Received(1).PublishAsync(Arg.Is<PublishRequest>(x => x.PhoneNumber == phone && x.Message == $"Your code is: {code}"));
        }
    }
}