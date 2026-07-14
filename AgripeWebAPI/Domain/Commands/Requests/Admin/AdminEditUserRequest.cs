using AgripeWebAPI.Domain.Commands.Responses.Admin;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace AgripeWebAPI.Domain.Commands.Requests.Admin
{
    public class AdminEditUserRequest : IRequest<AdminUserResponse>
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nome é obrigatório.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Email inválido.")]
        public string Email { get; set; } = string.Empty;

        [MinLength(8, ErrorMessage = "Senha deve ter no mínimo 8 caracteres.")]
        public string? Password { get; set; }

        public bool Active { get; set; }
        public bool IsAdmin { get; set; }

        /// <summary>Cria-se um agrônomo pelo admin — não há self-signup para o papel.</summary>
        public bool IsAgronomist { get; set; }
        public string? AgronomistCrea { get; set; }
    }
}
