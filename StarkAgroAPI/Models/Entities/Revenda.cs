namespace StarkAgroAPI.Models.Entities
{
    /// <summary>
    /// Revenda (loja de insumos) que compra o plano da StarkAgro e oferece o software à sua
    /// carteira de agricultores. É uma camada organizacional acima de agrônomos + produtores;
    /// a cobrança sobe para cá (o plano é vendido à revenda, não ao produtor-membro).
    /// </summary>
    public class Revenda : Entity
    {
        public string Name { get; set; } = string.Empty;

        public string? Cnpj { get; set; }

        public string? ContactEmail { get; set; }

        /// <summary>Plano vendido à revenda (<see cref="DiagnosisPlan"/>). null = sem plano.</summary>
        public int? DiagnosisPlanId { get; set; }

        /// <summary>Cota-padrão herdada pelos produtores-membros. null = padrão da plataforma.</summary>
        public int? DiagnosisQuotaPerMonth { get; set; }

        /// <summary>Soft-disable: preserva o histórico, igual ao plano.</summary>
        public bool Active { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public int CreatedByAdminId { get; set; }
    }
}
