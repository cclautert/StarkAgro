using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Revenda
{
    public class GetMyRevendaRequest : IRequest<RevendaResponse?>
    {
    }

    public class ListRevendaMembersRequest : IRequest<List<RevendaMemberResponse>>
    {
    }

    public class InviteRevendaMemberRequest : IRequest<RevendaMemberResponse?>
    {
        [Required(ErrorMessage = "Email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Email inválido.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>Papel do convidado: "Agronomist" ou "Client" (Manager é designado pelo admin).</summary>
        [Required(ErrorMessage = "Role é obrigatório.")]
        public string Role { get; set; } = string.Empty;
    }

    public class RevokeRevendaMemberRequest : IRequest<bool>
    {
        public int LinkId { get; set; }
    }
}
