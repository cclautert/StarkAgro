using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class EditUserHandler : IRequestHandler<EditUserRequest, EditUserResponse>
    {
        private readonly agpDBContext _dbContext;

        public EditUserHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<EditUserResponse> Handle(EditUserRequest request, CancellationToken cancellationToken)
        {
            var user = _dbContext.Users.Update(new User { Name = request.Name, Email = request.Email, Password = request.Password });
            await _dbContext.SaveChangesAsync();

            return new EditUserResponse { Id = user.Entity.Id, Name = user.Entity.Name, Email = user.Entity.Email };
        }
    }
}
