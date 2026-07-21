using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Revenda
{
    /// <summary>Assentos da revenda do gestor autenticado. A revenda vem do token, nunca do request.</summary>
    public class GetMyRevendaSeatsRequest : IRequest<RevendaSeatsResponse?>
    {
    }
}
