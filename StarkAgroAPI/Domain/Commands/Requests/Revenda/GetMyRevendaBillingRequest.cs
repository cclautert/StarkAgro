using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Revenda
{
    /// <summary>Fatura da revenda que o chamador gere (resolvida pelo token, não pelo request).</summary>
    public class GetMyRevendaBillingRequest : IRequest<RevendaBillingResponse?>
    {
    }
}
