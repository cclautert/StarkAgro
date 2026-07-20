using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    public class AdminDeleteUserHandler : IRequestHandler<AdminDeleteUserRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public AdminDeleteUserHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<bool> Handle(AdminDeleteUserRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users.Find(u => u.Id == request.Id).FirstOrDefaultAsync(cancellationToken);
            if (user == null)
            {
                _notifier.Handle(new Notification("Usuário não encontrado."));
                return false;
            }

            await _dbContext.Users.DeleteOneAsync(u => u.Id == request.Id, cancellationToken);
            return true;
        }
    }
}
