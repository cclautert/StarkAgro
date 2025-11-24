using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class DeleteUserHandler : IRequestHandler<DeleteUserRequest, DeleteUserResponse>
    {
        private readonly agpDBContext _dbContext;
        public DeleteUserHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public Task<DeleteUserResponse> Handle(DeleteUserRequest request, CancellationToken cancellationToken)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.Id == request.Id);
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {request.Id} not found.");
            }

            _dbContext.Users.Remove(user);
            _dbContext.SaveChanges();

            return Task.FromResult(new DeleteUserResponse { Success = true });
        }
    }
}
