using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StarkAgroAPI.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task<string> GenerateTokenAsync(User user, CancellationToken cancellationToken = default)
        {
            var claims = new List<Claim>
            {
                new Claim("id", user.Id.ToString()),
                new Claim("name", user.Name ?? string.Empty),
                new Claim("email", user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                // Claims booleanas mantidas (derivadas de Roles) para não quebrar a UI/guards atuais.
                new Claim("isAdmin", user.IsAdmin.ToString().ToLower()),
                new Claim("isAgronomist", user.IsAgronomist.ToString().ToLower()),
                new Claim("isResellerManager", user.IsResellerManager.ToString().ToLower())
            };

            // Uma claim "role" por papel — habilita RequireRole/HasRole daqui pra frente.
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim("role", role));
            }

            var secretKey = _configuration["JwtSettings:secretkey"] ?? throw new InvalidOperationException("JWT secret key must be configured.");
            var privateKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(privateKey, SecurityAlgorithms.HmacSha256);
            var expiration = DateTime.UtcNow.AddHours(8);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: expiration,
                signingCredentials: credentials
            );

            return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
        }
    }
}
