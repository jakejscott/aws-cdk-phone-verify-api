using Amazon.Lambda.Core;

namespace AwsCdkPhoneVerifyApi
{
    public class StartRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
}
