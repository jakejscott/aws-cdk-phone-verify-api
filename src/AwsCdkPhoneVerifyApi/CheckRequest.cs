using System;

namespace AwsCdkPhoneVerifyApi
{
    public class CheckRequest
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
    }
}
