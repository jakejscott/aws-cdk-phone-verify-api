using Amazon.Lambda.APIGatewayEvents;
using Amazon.SimpleNotificationService;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace AwsCdkPhoneVerifyApi.Tests
{
    public abstract class BaseLambdaTest
    {
        public const string phone = "+64223062141";

        protected IAmazonSimpleNotificationService sns;
        protected IVerificationsRepository repo;

        [SetUp]
        public virtual void SetUp()
        {
            sns = Substitute.For<IAmazonSimpleNotificationService>();
            repo = Substitute.For<IVerificationsRepository>();
        }

        protected APIGatewayProxyRequest CreateRequest<T>(T value) => new APIGatewayProxyRequest { Body = JsonConvert.SerializeObject(value) };
    }
}