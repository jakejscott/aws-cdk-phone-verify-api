using System;
using System.Threading.Tasks;

namespace AwsCdkPhoneVerifyApi
{
    public interface IVerificationsRepository
    {
        Task<long?> GetLatestVersionAsync(string phone);
        Task<Verification> GetVerificationAsync(Guid id);
        Task<Verification> GetVerificationAsync(string phone, long version);
        Task IncrementAttemptsAsync(string phone, long version);
        Task<long?> InsertInitialVersionAsync(string phone);
        Task<Verification> InsertNextVersionAsync(string phone, long currentVersion);
        Task SetVerifiedAsync(string phone, long version);
    }
}