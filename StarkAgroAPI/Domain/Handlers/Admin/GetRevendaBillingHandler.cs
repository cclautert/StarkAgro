using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Revenda;
using MediatR;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    public class GetRevendaBillingHandler : IRequestHandler<GetRevendaBillingRequest, RevendaBillingResponse?>
    {
        private readonly IRevendaBillingService _billing;
        private readonly INotifier _notifier;

        public GetRevendaBillingHandler(IRevendaBillingService billing, INotifier notifier)
        {
            _billing = billing;
            _notifier = notifier;
        }

        public async Task<RevendaBillingResponse?> Handle(GetRevendaBillingRequest request, CancellationToken cancellationToken)
        {
            var invoice = await _billing.GetRevendaInvoiceAsync(request.RevendaId, cancellationToken);
            if (invoice is null)
            {
                _notifier.Handle(new Notification("Revenda não encontrada."));
                return null;
            }

            return RevendaBillingResponse.From(invoice);
        }
    }
}
