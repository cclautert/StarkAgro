using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Services.Sentinel1;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    public class GetPlatformAiSettingsHandler : IRequestHandler<GetPlatformAiSettingsRequest, AdminAiSettingsResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IDiagnosisCostService _costService;
        private readonly INdviCostService _ndviCostService;
        private readonly ISentinel1CostService _s1CostService;

        public GetPlatformAiSettingsHandler(
            agpDBContext dbContext, IDiagnosisCostService costService,
            INdviCostService ndviCostService, ISentinel1CostService s1CostService)
        {
            _dbContext = dbContext;
            _costService = costService;
            _ndviCostService = ndviCostService;
            _s1CostService = s1CostService;
        }

        public async Task<AdminAiSettingsResponse> Handle(GetPlatformAiSettingsRequest request, CancellationToken cancellationToken)
        {
            var monthCost = await _costService.GetCurrentMonthCostCentsAsync(cancellationToken);
            var ndviMonthCost = await _ndviCostService.GetCurrentMonthCostCentsAsync(cancellationToken);
            var s1MonthCost = await _s1CostService.GetCurrentMonthCostCentsAsync(cancellationToken);

            var settings = await _dbContext.PlatformAiSettings.Find(x => x.Id == 1).FirstOrDefaultAsync(cancellationToken);

            if (settings == null)
                return new AdminAiSettingsResponse
                {
                    ActiveProvider = "gemini",
                    CurrentMonthAiCostCents = monthCost,
                    CurrentMonthNdviCostCents = ndviMonthCost,
                    CurrentMonthSentinel1CostCents = s1MonthCost
                };

            return new AdminAiSettingsResponse
            {
                OpenAiKey = settings.OpenAiKey,
                OpenAiModel = settings.OpenAiModel,
                AnthropicKey = settings.AnthropicKey,
                AnthropicModel = settings.AnthropicModel,
                GeminiKey = settings.GeminiKey,
                GeminiModel = settings.GeminiModel,
                ActiveProvider = settings.ActiveProvider,
                CropHealthKey = settings.CropHealthKey,
                CropHealthEnabled = settings.CropHealthEnabled,
                DefaultDiagnosisQuotaPerMonth = settings.DefaultDiagnosisQuotaPerMonth,
                CropHealthCostCents = settings.CropHealthCostCents,
                CurrentMonthAiCostCents = monthCost,
                CdseClientId = settings.CdseClientId,
                CdseClientSecret = settings.CdseClientSecret,
                Sentinel2Enabled = settings.Sentinel2Enabled,
                ExtraIndicesEnabled = settings.ExtraIndicesEnabled,
                FirmsMapKey = settings.FirmsMapKey,
                FireAlertsEnabled = settings.FireAlertsEnabled,
                FireAlertRadiusKm = settings.FireAlertRadiusKm,
                ClimateAlertsEnabled = settings.ClimateAlertsEnabled,
                FrostAlertTempC = settings.FrostAlertTempC,
                HeatAlertTempC = settings.HeatAlertTempC,
                Sentinel1Enabled = settings.Sentinel1Enabled,
                Sentinel1CostCents = settings.Sentinel1CostCents,
                CurrentMonthSentinel1CostCents = s1MonthCost,
                NdviCostCents = settings.NdviCostCents,
                NdviMonthlyBudgetCents = settings.NdviMonthlyBudgetCents,
                NdviMaxAreasPerUser = settings.NdviMaxAreasPerUser,
                CurrentMonthNdviCostCents = ndviMonthCost
            };
        }
    }
}
