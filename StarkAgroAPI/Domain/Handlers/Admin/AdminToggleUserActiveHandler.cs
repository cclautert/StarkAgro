using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    public class AdminToggleUserActiveHandler : IRequestHandler<AdminToggleUserActiveRequest, AdminUserResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public AdminToggleUserActiveHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<AdminUserResponse> Handle(AdminToggleUserActiveRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users.Find(u => u.Id == request.Id).FirstOrDefaultAsync(cancellationToken);
            if (user == null)
            {
                _notifier.Handle(new Notification("Usuário não encontrado."));
                return null!;
            }

            var update = Builders<Models.Entities.User>.Update.Set(u => u.Active, request.Active);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == request.Id, update, cancellationToken: cancellationToken);

            user.Active = request.Active;
            return new AdminUserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Active = user.Active,
                IsAdmin = user.IsAdmin
            };
        }
    }
}
