using System;

namespace AwsCdkPhoneVerifyApi.CheckLambda
{
    public class CheckRequest
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
    }
}
