using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class CreateUserHandler : IRequestHandler<CreateUserRequest, CreateUserResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreateUserHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<CreateUserResponse> Handle(CreateUserRequest request, CancellationToken cancellationToken)
        {
            var user = _dbContext.Users.Add(new User { Name = request.Name, Email = request.Email, Password = request.Password });
            await _dbContext.SaveChangesAsync();

            return new CreateUserResponse { Id = user.Entity.Id, Name = user.Entity.Name, Email = user.Entity.Email };
        }
    }
}
