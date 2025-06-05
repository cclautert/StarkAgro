using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class GetToken : IRequestHandler<UserTokenRequest, UserTokenResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IConfiguration _configuration;

        public GetToken(agpDBContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration;
        }

        public async Task<UserTokenResponse> Handle(UserTokenRequest request, CancellationToken cancellationToken)
        {
            User? user = await _dbContext.Users
                .Where(x => x.Email == request.Email && x.Password == request.Password)
                .Select(x => new User
                {
                    Id = x.Id,
                    Name = x.Name,
                    Email = x.Email
                }).SingleOrDefaultAsync(cancellationToken);

            if (user == null)
                return null;

            return new UserTokenResponse { Token = await GenerateToken(user, cancellationToken) };
        }

        private async Task<string> GenerateToken(User user, CancellationToken cancellationToken)
        {
            var realUser = setUserData(user);

            var claims = new List<Claim>
            {
                new Claim("User", JsonSerializer.Serialize(realUser, new JsonSerializerOptions  { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var provateKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:secretkey"]));
            var credentials = new SigningCredentials(provateKey, SecurityAlgorithms.HmacSha256);
            var expiration = DateTime.UtcNow.AddHours(8);

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: expiration,
                signingCredentials: credentials
            );

            return await Task.Run(() => new JwtSecurityTokenHandler().WriteToken(token));
        }

        private object setUserData(User user)
        {
            return new
            {
                userId = user?.Id.ToString() ?? String.Empty,
                name = user?.Name ?? String.Empty,
                email = user?.Email ?? String.Empty
            };
        }
    }
}
