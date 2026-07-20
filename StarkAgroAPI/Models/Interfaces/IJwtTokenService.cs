using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Models.Interfaces
{
    public interface IJwtTokenService
    {
        Task<string> GenerateTokenAsync(User user, CancellationToken cancellationToken = default);
    }
}
