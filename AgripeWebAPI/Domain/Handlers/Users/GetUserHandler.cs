using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class GetUserHandler : IRequestHandler<GetUserRequest, GetUserResponse>
    {
        private readonly agpDBContext _dbContext;

        public GetUserHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<GetUserResponse?> Handle(GetUserRequest request, CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .Where(x => x.Name == request.Name)
                .Select(x => new GetUserResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Email = x.Email
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
