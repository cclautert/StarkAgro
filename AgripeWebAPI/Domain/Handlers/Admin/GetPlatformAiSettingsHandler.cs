using AgripeWebAPI.Domain.Commands.Requests.Admin;
using AgripeWebAPI.Domain.Commands.Responses.Admin;
using AgripeWebAPI.Models;
using AgripeWebAPI.Services.Diagnosis;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Admin
{
    public class GetPlatformAiSettingsHandler : IRequestHandler<GetPlatformAiSettingsRequest, AdminAiSettingsResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IDiagnosisCostService _costService;

        public GetPlatformAiSettingsHandler(agpDBContext dbContext, IDiagnosisCostService costService)
        {
            _dbContext = dbContext;
            _costService = costService;
        }

        public async Task<AdminAiSettingsResponse> Handle(GetPlatformAiSettingsRequest request, CancellationToken cancellationToken)
        {
            var monthCost = await _costService.GetCurrentMonthCostCentsAsync(cancellationToken);

            var settings = await _dbContext.PlatformAiSettings.Find(x => x.Id == 1).FirstOrDefaultAsync(cancellationToken);

            if (settings == null)
                return new AdminAiSettingsResponse { ActiveProvider = "gemini", CurrentMonthAiCostCents = monthCost };

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
                CurrentMonthAiCostCents = monthCost
            };
        }
    }
}
