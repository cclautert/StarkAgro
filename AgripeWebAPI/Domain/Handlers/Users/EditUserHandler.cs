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
            var user = _dbContext.Users.Where(x => x.Email == request.Email).FirstOrDefault();

            if (user == null)
            {
                throw new Exception("Email não encontrado.");
            }

            user.Name = request.Name;
            user.Email = request.Email;
            user.Password = request.Password;
                
            var userUpdated = _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            return new EditUserResponse { Id = userUpdated.Entity.Id, Name = userUpdated.Entity.Name, Email = userUpdated.Entity.Email };
        }
    }
}
