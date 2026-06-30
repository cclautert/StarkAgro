using AgripeWebAPI.Domain.Commands.Responses.Admin;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace AgripeWebAPI.Domain.Commands.Requests.Admin
{
    public class AdminCreateUserRequest : IRequest<AdminUserResponse>
    {
        [Required(ErrorMessage = "Nome é obrigatório.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Email inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha é obrigatória.")]
        [MinLength(8, ErrorMessage = "Senha deve ter no mínimo 8 caracteres.")]
        public string Password { get; set; } = string.Empty;

        public bool Active { get; set; } = true;
        public bool IsAdmin { get; set; } = false;
    }
}
