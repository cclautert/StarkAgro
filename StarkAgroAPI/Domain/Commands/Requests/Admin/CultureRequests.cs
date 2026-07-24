using StarkAgroAPI.Domain.Commands.Responses.Admin;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
{
    /// <summary>Lista de nomes de culturas (ordenada) — pública para qualquer usuário autenticado.</summary>
    public class GetCulturesRequest : IRequest<List<string>>
    {
    }

    /// <summary>Lista com id+nome — para o CRUD do admin.</summary>
    public class GetAdminCulturesRequest : IRequest<List<CultureResponse>>
    {
    }

    public class CreateCultureRequest : IRequest<CultureResponse?>
    {
        [Required]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "O nome da cultura deve ter entre 1 e 80 caracteres.")]
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateCultureRequest : IRequest<CultureResponse?>
    {
        public int Id { get; set; }

        [Required]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "O nome da cultura deve ter entre 1 e 80 caracteres.")]
        public string Name { get; set; } = string.Empty;
    }

    public class DeleteCultureRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }
}
