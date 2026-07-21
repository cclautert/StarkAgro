namespace StarkAgroAPI.Models.Entities
{
    /// <summary>
    /// Plano mensal de laudos de um produtor. Mensalidade fixa que já inclui N laudos; o que
    /// passar disso é excedente cobrado à parte.
    /// <para>
    /// Os preços vivem <b>aqui</b>, no banco, editáveis em <c>/admin</c> — nunca cravados em
    /// código. Assim o dono do negócio muda a tabela sem redeploy, e o mesmo padrão das chaves
    /// de IA e da cota vale para o dinheiro. Valores em <b>centavos inteiros</b>: dinheiro em
    /// <c>double</c> acumula erro.
    /// </para>
    /// </summary>
    public class DiagnosisPlan : Entity
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Mensalidade do plano, em centavos.</summary>
        public int MonthlyPriceCents { get; set; }

        /// <summary>Laudos já inclusos na mensalidade. <c>0</c> = nenhum incluso (tudo é excedente).</summary>
        public int IncludedReportsPerMonth { get; set; }

        /// <summary>Preço de cada laudo além do incluso, em centavos.</summary>
        public int OveragePriceCents { get; set; }

        /// <summary>
        /// Produtores inclusos na mensalidade quando o plano é vendido a uma revenda.
        /// <c>0</c> = nenhum incluso. Só faz sentido em plano de revenda; num plano de produtor
        /// individual fica zerado e é ignorado.
        /// </summary>
        public int IncludedMembers { get; set; }

        /// <summary>Preço de cada produtor além do incluso, em centavos.</summary>
        public int MemberOveragePriceCents { get; set; }

        /// <summary>
        /// Teto duro de produtores da revenda — atingido, o convite é recusado.
        /// <c>0</c> = ilimitado, mesma convenção de <see cref="PlatformAiSettings.NdviMaxAreasPerUser"/>.
        /// <para>
        /// Assento é o eixo <b>comercial</b> e laudo é o eixo de <b>custo</b>: o excedente de laudo é
        /// cobrado, não bloqueado; o de assento é cobrado até o teto e bloqueado a partir dele.
        /// </para>
        /// </summary>
        public int MaxMembers { get; set; }

        /// <summary>Plano inativo não pode ser atribuído a novos produtores, mas preserva o histórico.</summary>
        public bool Active { get; set; } = true;
    }
}
