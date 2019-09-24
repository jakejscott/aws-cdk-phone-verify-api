using System;
using System.Linq;
using NSubstitute;
using NUnit.Framework;

namespace AwsCdkPhoneVerifyApi.Tests
{
    [TestFixture]
    public class RateLimitTests : BaseLambdaTest
    {
        [Test]
        public void TenInTheLast30Min()
        {
            // The top is 10 every half an hour, per phone number.
            var limit = 10;
            var period = TimeSpan.FromMilliseconds(30);

            // With 10 verifications in the last 30 mins
            var verifications = Enumerable.Repeat(new Verification { Created = DateTime.UtcNow }, 10).ToList();
            repo.GetLatestVerificationsAsync(phone, limit).Returns(verifications);

            var rateLimit = RateLimitHelper.HasExceeededRateLimit(verifications, limit, DateTimeOffset.UtcNow - period);
            Assert.True(rateLimit);
        }

        [Test]
        public void NineInTheLastThirtyMin()
        {
            // The top is 10 every half an hour, per phone number.
            var limit = 10;
            var period = TimeSpan.FromMilliseconds(30);

            // With 9 verifications in the last 30 mins
            var verifications = Enumerable.Repeat(new Verification {Created = DateTime.UtcNow}, 9).ToList();
            repo.GetLatestVerificationsAsync(phone, limit).Returns(verifications);

            var rateLimit = RateLimitHelper.HasExceeededRateLimit(verifications, limit, DateTimeOffset.UtcNow - period);
            Assert.False(rateLimit);
        }

        [Test]
        public void TenThatWereCreatedThirtyMinAgo()
        {
            // The top is 10 every half an hour, per phone number.
            var limit = 10;
            var period = TimeSpan.FromMilliseconds(30);

            // With 10 verifications that were created 30 mins ago
            var verifications = Enumerable.Repeat(new Verification { Created = DateTime.UtcNow - TimeSpan.FromMinutes(30) }, 10).ToList();
            repo.GetLatestVerificationsAsync(phone, limit).Returns(verifications);

            var rateLimit = RateLimitHelper.HasExceeededRateLimit(verifications, limit, DateTimeOffset.UtcNow - period);
            Assert.False(rateLimit);
        }
    }
}