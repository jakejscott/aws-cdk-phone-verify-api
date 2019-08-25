using System;

namespace AwsCdkPhoneVerifyApi
{
    public class StatusResponse
    {
        public Guid Id { get; set; }
        public string Phone { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Verified { get; set; }
    }
}
