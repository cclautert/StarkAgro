using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Ndvi;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Ndvi
{
    /// <summary>
    /// Prescrição de adubação de uma passagem: cruza o perfil NPK da cultura (dose kg/ha por classe)
    /// com a distribuição de classes da passagem (<c>ClassCounts</c>) e os hectares do talhão, dando
    /// quanto de N/P/K cada zona precisa e o total. <b>Custo zero de CDSE</b> — tudo já está no banco.
    /// Posse dupla (área do dono → reading da área), verbatim de <see cref="GetNdviTrendHandler"/>.
    /// </summary>
    public class GetFertilizationPrescriptionHandler
        : IRequestHandler<GetFertilizationPrescriptionRequest, FertilizationPrescriptionResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public GetFertilizationPrescriptionHandler(
            agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<FertilizationPrescriptionResponse?> Handle(
            GetFertilizationPrescriptionRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            // 1) Posse: área do chamador.
            var area = await _dbContext.MonitoredAreas
                .Find(a => a.Id == request.AreaId && a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null)
            {
                _notifier.Handle(new Notification("Área não encontrada."));
                return null;
            }

            // 2) Reading dessa área e do chamador.
            var reading = await _dbContext.NdviReadings
                .Find(r => r.Id == request.ReadingId && r.AreaId == area.Id && r.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (reading is null)
            {
                _notifier.Handle(new Notification("Passagem não encontrada."));
                return null;
            }

            // 3) Elegibilidade: precisa da distribuição por classe (nublada/legado não tem).
            var totalPixels = reading.ClassCounts?.Sum(c => c.PixelCount) ?? 0;
            if (reading.CloudRejected || reading.ClassCounts is null || reading.ClassCounts.Count == 0 || totalPixels <= 0)
            {
                _notifier.Handle(new Notification("Esta passagem não tem distribuição de zonas para prescrever (nublada ou anterior à classificação)."));
                return null;
            }

            // 4) Área do talhão.
            var totalHa = AreaHectares.Of(area);
            if (totalHa <= 0)
            {
                _notifier.Handle(new Notification("Não foi possível calcular a área do talhão."));
                return null;
            }

            // 5) Perfil: explícito (override) ou automático pela cultura da área. Regra compartilhada
            //    com o GeoTIFF de doses — não pode divergir.
            var profiles = await _dbContext.FertilizationProfiles.Find(_ => true).ToListAsync(cancellationToken);
            var (profile, profileError) = FertilizationProfileResolver.Resolve(profiles, area, request.ProfileId);
            if (profile is null)
            {
                _notifier.Handle(new Notification(profileError!));
                return null;
            }

            // 6) Monta as zonas na ordem da classificação; percentuais pela mesma base do gráfico.
            var ordered = NdviClassification.Classes
                .Select(c => (Class: c, Count: reading.ClassCounts.FirstOrDefault(x => x.Key == c.Key)?.PixelCount ?? 0))
                .ToList();
            var percentages = NdviClassification.ToPercentages([.. ordered.Select(o => o.Count)]);

            var zones = new List<PrescriptionZone>();
            double totalN = 0, totalP = 0, totalK = 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                var (cls, count) = ordered[i];
                if (count <= 0) continue; // só zonas presentes na passagem

                var ha = totalHa * count / totalPixels;
                var dose = profile.Doses.FirstOrDefault(d => d.ClassKey == cls.Key);
                var hasDose = dose is not null;

                var nHa = dose?.NitrogenKgHa ?? 0;
                var pHa = dose?.PhosphorusKgHa ?? 0;
                var kHa = dose?.PotassiumKgHa ?? 0;
                var nKg = nHa * ha; var pKg = pHa * ha; var kKg = kHa * ha;
                totalN += nKg; totalP += pKg; totalK += kKg;

                zones.Add(new PrescriptionZone
                {
                    ClassKey = cls.Key,
                    Label = cls.Label,
                    Color = cls.HexColor,
                    PixelCount = count,
                    Percent = Math.Round(percentages[i], 2),
                    Hectares = Math.Round(ha, 3),
                    NitrogenKgHa = nHa,
                    PhosphorusKgHa = pHa,
                    PotassiumKgHa = kHa,
                    NitrogenKg = Math.Round(nKg, 1),
                    PhosphorusKg = Math.Round(pKg, 1),
                    PotassiumKg = Math.Round(kKg, 1),
                    HasDose = hasDose
                });
            }

            return new FertilizationPrescriptionResponse
            {
                AreaId = area.Id,
                ReadingId = reading.Id,
                AcquisitionDate = reading.AcquisitionDate,
                Culture = profile.Culture,
                ProfileId = profile.Id,
                TotalHectares = Math.Round(totalHa, 3),
                CloudCoveragePct = reading.CloudCoveragePct,
                Zones = zones,
                TotalNitrogenKg = Math.Round(totalN, 1),
                TotalPhosphorusKg = Math.Round(totalP, 1),
                TotalPotassiumKg = Math.Round(totalK, 1)
            };
        }
    }
}
