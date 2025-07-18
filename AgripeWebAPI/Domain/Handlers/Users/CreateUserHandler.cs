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
            var user = _dbContext.Users.Where(x => x.Email == request.Email).FirstOrDefault();

            if (user != null)
            {
                throw new Exception("Email já cadastrado.");
            }

            var userUpdated = _dbContext.Users.Add(new User { Name = request.Name, Email = request.Email, Password = request.Password, Active = true });
            await _dbContext.SaveChangesAsync();

            return new CreateUserResponse { Id = userUpdated.Entity.Id, Name = userUpdated.Entity.Name, Email = userUpdated.Entity.Email };
        }
    }
}
