namespace StarkAgroAPI.Domain.Commands.Responses.Ndvi
{
    /// <summary>
    /// Uma zona (classe de biomassa) da prescrição: a dose configurada no perfil × os hectares
    /// que essa classe ocupa na passagem. Rótulo e cor viajam junto (mesma disciplina das classes
    /// do NDVI: o front não duplica a tabela de cores).
    /// </summary>
    public class PrescriptionZone
    {
        public string ClassKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        public long PixelCount { get; set; }
        /// <summary>Percentual da área válida da passagem (0-100).</summary>
        public double Percent { get; set; }
        public double Hectares { get; set; }

        // Dose do perfil (kg/ha) e o total da zona (dose × hectares).
        public double NitrogenKgHa { get; set; }
        public double PhosphorusKgHa { get; set; }
        public double PotassiumKgHa { get; set; }
        public double NitrogenKg { get; set; }
        public double PhosphorusKg { get; set; }
        public double PotassiumKg { get; set; }

        /// <summary><c>false</c> quando o perfil não tem dose para esta classe — a zona entra com
        /// zero e a tela marca "sem dose", em vez de sumir em silêncio.</summary>
        public bool HasDose { get; set; }
    }

    /// <summary>
    /// Prescrição de adubação de uma passagem: cruza o perfil de NPK da cultura com a distribuição
    /// de classes (<c>ClassCounts</c>) e a área do talhão. Derivado de dado já armazenado — custo
    /// zero de CDSE. <see cref="CloudCoveragePct"/> viaja para a tela sinalizar imprecisão quando a
    /// passagem tinha nuvem (a área é distribuída só entre os pixels válidos).
    /// </summary>
    public class FertilizationPrescriptionResponse
    {
        public int AreaId { get; set; }
        public int ReadingId { get; set; }
        public System.DateTime AcquisitionDate { get; set; }

        /// <summary>Cultura do perfil aplicado.</summary>
        public string Culture { get; set; } = string.Empty;
        public int ProfileId { get; set; }

        public double TotalHectares { get; set; }
        public double CloudCoveragePct { get; set; }

        public List<PrescriptionZone> Zones { get; set; } = [];

        public double TotalNitrogenKg { get; set; }
        public double TotalPhosphorusKg { get; set; }
        public double TotalPotassiumKg { get; set; }
    }
}
