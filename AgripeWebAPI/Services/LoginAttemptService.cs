using Microsoft.Extensions.Caching.Memory;

namespace AgripeWebAPI.Services
{
    public interface ILoginAttemptService
    {
        bool IsLockedOut(string email);
        void RecordFailure(string email);
        void ResetFailures(string email);
    }

    public class LoginAttemptService : ILoginAttemptService
    {
        private const int MaxAttempts = 5;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

        private readonly IMemoryCache _cache;

        public LoginAttemptService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool IsLockedOut(string email)
        {
            var key = CacheKey(email);
            if (!_cache.TryGetValue(key, out LoginAttemptRecord? record) || record is null)
                return false;

            return record.Count >= MaxAttempts && DateTime.UtcNow < record.WindowStart + Window;
        }

        public void RecordFailure(string email)
        {
            var key = CacheKey(email);
            _cache.TryGetValue(key, out LoginAttemptRecord? record);

            if (record is null || DateTime.UtcNow >= record.WindowStart + Window)
                record = new LoginAttemptRecord(1, DateTime.UtcNow);
            else
                record = record with { Count = record.Count + 1 };

            _cache.Set(key, record, Window);
        }

        public void ResetFailures(string email)
        {
            _cache.Remove(CacheKey(email));
        }

        private static string CacheKey(string email) =>
            $"login_fail_{email.ToLowerInvariant()}";

        private record LoginAttemptRecord(int Count, DateTime WindowStart);
    }
}
