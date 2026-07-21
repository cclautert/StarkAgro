using StarkAgroAPI.Domain.Commands.Requests.Revenda;
using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Revenda;
using MediatR;

namespace StarkAgroAPI.Domain.Handlers.Revenda
{
    /// <summary>
    /// Ocupação de assentos da revenda do gestor. Endpoint separado do faturamento de propósito:
    /// a tela de membros só precisa da contagem, e a fatura percorre o consumo de cada cliente.
    /// </summary>
    public class GetMyRevendaSeatsHandler : IRequestHandler<GetMyRevendaSeatsRequest, RevendaSeatsResponse?>
    {
        private readonly ICurrentUserContext _currentUser;
        private readonly IRevendaMembershipService _membership;
        private readonly IRevendaSeatService _seats;
        private readonly INotifier _notifier;

        public GetMyRevendaSeatsHandler(ICurrentUserContext currentUser,
            IRevendaMembershipService membership, IRevendaSeatService seats, INotifier notifier)
        {
            _currentUser = currentUser;
            _membership = membership;
            _seats = seats;
            _notifier = notifier;
        }

        public async Task<RevendaSeatsResponse?> Handle(GetMyRevendaSeatsRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var revendaId = await _membership.GetManagedRevendaIdAsync(userId, cancellationToken);
            if (revendaId is null)
            {
                _notifier.Handle(new Notification("Você não gere nenhuma revenda."));
                return null;
            }

            return RevendaSeatsResponse.From(await _seats.GetAsync(revendaId.Value, cancellationToken));
        }
    }
}
