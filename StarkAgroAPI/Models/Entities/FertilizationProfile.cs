namespace StarkAgroAPI.Models.Entities
{
    /// <summary>
    /// Dose de NPK (kg/ha) para uma classe de biomassa. <see cref="ClassKey"/> casa com
    /// <c>NdviClassification.Classes[].Key</c> — a zona do GeoTIFF vira uma prescrição.
    /// </summary>
    public class ZoneDose
    {
        public string ClassKey { get; set; } = string.Empty;
        public double NitrogenKgHa { get; set; }
        public double PhosphorusKgHa { get; set; }
        public double PotassiumKgHa { get; set; }
    }

    /// <summary>
    /// Perfil de adubação por cultura: a dose de NPK que cada zona (classe de biomassa) recebe.
    /// <b>Dado global de plataforma</b> (como <c>DiagnosisPlan</c>), gerido só pelo admin — não é
    /// por-tenant. <b>Agronomia é parâmetro, não código</b>: a estrutura vem do sistema, os valores
    /// vêm do admin/agrônomo. Nasce sem doses.
    /// </summary>
    public class FertilizationProfile : Entity
    {
        public string Culture { get; set; } = string.Empty;
        public List<ZoneDose> Doses { get; set; } = [];
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
