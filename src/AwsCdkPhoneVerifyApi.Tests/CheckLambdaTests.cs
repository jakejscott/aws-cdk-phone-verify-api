using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using AwsCdkPhoneVerifyApi.CheckLambda;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using OtpNet;

namespace AwsCdkPhoneVerifyApi.Tests
{
    [TestFixture]
    public class CheckLambdaTests : BaseLambdaTest
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
            var startRequest = new CheckRequest { Id = Guid.NewGuid(), Code = "bogus" };
            var request = CreateRequest(startRequest);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(400, response.StatusCode);
            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Not found", error.Error);
        }

        [Test]
        public async Task RateLimit()
        {
            var id = Guid.NewGuid();
            var secret = Encoding.UTF8.GetBytes("secret");
            var hotp = new Hotp(secret);
            var code = hotp.ComputeHOTP(1);

            // Arrange
            var startRequest = new CheckRequest { Id = id, Code = code };
            var request = CreateRequest(startRequest);

            // Mock
            var verification = new Verification
            {
                Id = id,
                Phone = phone,
                Created = DateTime.UtcNow,
                Attempts = 3,
                SecretKey = secret,
                Version = 1
            };
            repo.GetVerificationAsync(id).Returns(verification);

            // Mock
            var verifications = Enumerable.Repeat(new Verification { Created = DateTime.UtcNow }, 20).ToList();
            repo.GetLatestVerificationsAsync(phone, 5).Returns(verifications);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(429, response.StatusCode);
            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Rate limit", error.Error);
        }

        [Test]
        public async Task AlreadyVerified()
        {
            var id = Guid.NewGuid();

            // Arrange
            var startRequest = new CheckRequest { Id = id, Code = "bogus" };
            var request = CreateRequest(startRequest);

            // Mock
            var verification = new Verification { Id = id, Verified = DateTime.UtcNow };
            repo.GetVerificationAsync(id).Returns(verification);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(400, response.StatusCode);
            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Already verified", error.Error);
        }

        [Test]
        public async Task Expired()
        {
            var id = Guid.NewGuid();

            // Arrange
            var startRequest = new CheckRequest { Id = id, Code = "bogus" };
            var request = CreateRequest(startRequest);

            // Mock
            var verification = new Verification
            {
                Id = id,
                Created = DateTime.UtcNow.AddMinutes(-5)
            };
            repo.GetVerificationAsync(id).Returns(verification);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(400, response.StatusCode);
            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Expired", error.Error);
        }

        [Test]
        public async Task AttemptsExceeded()
        {
            var id = Guid.NewGuid();

            // Arrange
            var startRequest = new CheckRequest { Id = id, Code = "bogus" };
            var request = CreateRequest(startRequest);

            // Mock
            var verification = new Verification
            {
                Id = id,
                Created = DateTime.UtcNow,
                Attempts = 4
            };
            repo.GetVerificationAsync(id).Returns(verification);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(400, response.StatusCode);
            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Attempts exceeded", error.Error);
        }

        [Test]
        public async Task InvalidCode()
        {
            var id = Guid.NewGuid();

            // Arrange
            var startRequest = new CheckRequest { Id = id, Code = "bogus" };
            var request = CreateRequest(startRequest);

            // Mock
            var verification = new Verification
            {
                Phone = phone,
                Id = id,
                Created = DateTime.UtcNow,
                Attempts = 3,
                SecretKey = Encoding.UTF8.GetBytes("secret"),
                Version = 1
            };
            repo.GetVerificationAsync(id).Returns(verification);

            // Mock
            var verifications = Enumerable.Empty<Verification>().ToList();
            repo.GetLatestVerificationsAsync(phone, 5).Returns(verifications);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(400, response.StatusCode);

            await repo.Received(1).IncrementAttemptsAsync(verification.Phone, verification.Version);

            var error = JsonConvert.DeserializeObject<ErrorResponse>(response.Body);
            Assert.AreEqual("Invalid code", error.Error);
        }

        [Test]
        public async Task Valid()
        {
            var id = Guid.NewGuid();
            var secret = Encoding.UTF8.GetBytes("secret");
            var hotp = new Hotp(secret);
            var code = hotp.ComputeHOTP(1);

            // Arrange
            var startRequest = new CheckRequest { Id = id, Code = code };
            var request = CreateRequest(startRequest);

            // Mock
            var verification = new Verification
            {
                Id = id,
                Phone = phone,
                Created = DateTime.UtcNow,
                Attempts = 3,
                SecretKey = secret,
                Version = 1
            };
            repo.GetVerificationAsync(id).Returns(verification);

            // Mock
            var verifications = Enumerable.Empty<Verification>().ToList();
            repo.GetLatestVerificationsAsync(phone, 5).Returns(verifications);

            // Act
            var response = await function.ExecuteAsync(request, new TestLambdaContext());

            // Assert
            Assert.AreEqual(200, response.StatusCode);
            await repo.Received(1).SetVerifiedAsync(phone, 1);

            var checkResponse = JsonConvert.DeserializeObject<CheckResponse>(response.Body);
            Assert.True(checkResponse.Verified);
        }
    }
}