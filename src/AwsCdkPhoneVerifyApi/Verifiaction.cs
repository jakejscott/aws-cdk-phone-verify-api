﻿using System;

namespace AwsCdkPhoneVerifyApi
{
    public class Verification
    {
        public Guid Id { get; set; }
        public string Phone { get; set; }
        public long Version { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Verified { get; set; }
        public int Attempts { get; set; }
    }
}