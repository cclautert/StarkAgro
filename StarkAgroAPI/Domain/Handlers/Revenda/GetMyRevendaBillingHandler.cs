using StarkAgroAPI.Domain.Commands.Requests.Revenda;
using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Revenda;
using MediatR;

namespace StarkAgroAPI.Domain.Handlers.Revenda
{
    public class GetMyRevendaBillingHandler : IRequestHandler<GetMyRevendaBillingRequest, RevendaBillingResponse?>
    {
        private readonly ICurrentUserContext _currentUser;
        private readonly IRevendaMembershipService _membership;
        private readonly IRevendaBillingService _billing;
        private readonly INotifier _notifier;

        public GetMyRevendaBillingHandler(ICurrentUserContext currentUser,
            IRevendaMembershipService membership, IRevendaBillingService billing, INotifier notifier)
        {
            _currentUser = currentUser;
            _membership = membership;
            _billing = billing;
            _notifier = notifier;
        }

        public async Task<RevendaBillingResponse?> Handle(GetMyRevendaBillingRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var revendaId = await _membership.GetManagedRevendaIdAsync(userId, cancellationToken);
            if (revendaId is null)
            {
                _notifier.Handle(new Notification("Você não gere nenhuma revenda."));
                return null;
            }

            var invoice = await _billing.GetRevendaInvoiceAsync(revendaId.Value, cancellationToken);
            if (invoice is null)
            {
                _notifier.Handle(new Notification("Revenda não encontrada."));
                return null;
            }

            return RevendaBillingResponse.From(invoice);
        }
    }
}
