namespace StarkAgroAPI.Models.Entities
{
    /// <summary>
    /// Cultura da lista canônica da plataforma (Soja, Milho, Café…). <b>Dado global</b> (como
    /// <see cref="FertilizationProfile"/>/<c>DiagnosisPlan</c>): gerido só pelo admin, sem tenant.
    /// Os seletores de cultura (área, perfil de adubação, diagnóstico) leem esta lista — é o que faz
    /// <c>MonitoredArea.Crop</c> casar exatamente com <c>FertilizationProfile.Culture</c>.
    /// </summary>
    public class Culture : Entity
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
