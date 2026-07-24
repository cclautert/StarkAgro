using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Resolve QUAL perfil de adubação aplicar a uma área: <c>profileId</c> explícito (override) vence;
    /// senão casa pela <see cref="MonitoredArea.Crop"/> (case-insensitive + trim). Puro e estático —
    /// a regra é <b>uma só</b> para o relatório e para o GeoTIFF de doses, que não podem divergir.
    /// </summary>
    public static class FertilizationProfileResolver
    {
        /// <returns>
        /// <c>(profile, null)</c> em sucesso; <c>(null, mensagem)</c> quando não há como resolver —
        /// o chamador notifica com a mensagem e retorna null.
        /// </returns>
        public static (FertilizationProfile? profile, string? error) Resolve(
            IReadOnlyList<FertilizationProfile> profiles, MonitoredArea area, int? profileId)
        {
            if (profileId is int pid)
            {
                var byId = profiles.FirstOrDefault(p => p.Id == pid);
                return byId is null
                    ? (null, "Perfil de adubação não encontrado.")
                    : (byId, null);
            }

            if (string.IsNullOrWhiteSpace(area.Crop))
                return (null, "A área não tem cultura definida — edite a área ou escolha um perfil de adubação.");

            var byCulture = profiles.FirstOrDefault(p =>
                string.Equals(p.Culture.Trim(), area.Crop!.Trim(), StringComparison.OrdinalIgnoreCase));

            return byCulture is null
                ? (null, $"Nenhum perfil de adubação para a cultura \"{area.Crop}\" — cadastre em /admin/adubacao.")
                : (byCulture, null);
        }
    }
}
