using System;
using System.Collections.Generic;
using System.Linq;

namespace AwsCdkPhoneVerifyApi
{
    public class RateLimitHelper
    {
        public static bool HasExceeededRateLimit(List<Verification> verifications, int limit, DateTimeOffset min)
        {
            var count = verifications.Count(x => x.Created >= min);
            return count >= limit;
        }
    }
}