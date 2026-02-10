using AgripeWebAPI.Models.Entities;

namespace AgripeWebAPI.Models.Interfaces
{
    public interface IJwtTokenService
    {
        Task<string> GenerateTokenAsync(User user, CancellationToken cancellationToken = default);
    }
}
