using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
{
    /// <summary>Fatura de uma revenda específica (visão do admin).</summary>
    public class GetRevendaBillingRequest : IRequest<RevendaBillingResponse?>
    {
        public int RevendaId { get; set; }
    }
}
