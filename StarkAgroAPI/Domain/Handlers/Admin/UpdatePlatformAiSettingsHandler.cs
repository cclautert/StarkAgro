using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    public class UpdatePlatformAiSettingsHandler : IRequestHandler<UpdatePlatformAiSettingsRequest, bool>
    {
        private readonly agpDBContext _dbContext;

        public UpdatePlatformAiSettingsHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> Handle(UpdatePlatformAiSettingsRequest request, CancellationToken cancellationToken)
        {
            var entity = new PlatformAiSettings
            {
                Id = 1,
                OpenAiKey = request.OpenAiKey,
                OpenAiModel = request.OpenAiModel,
                AnthropicKey = request.AnthropicKey,
                AnthropicModel = request.AnthropicModel,
                GeminiKey = request.GeminiKey,
                GeminiModel = request.GeminiModel,
                ActiveProvider = request.ActiveProvider,
                CropHealthKey = request.CropHealthKey,
                CropHealthEnabled = request.CropHealthEnabled,
                DefaultDiagnosisQuotaPerMonth = request.DefaultDiagnosisQuotaPerMonth,
                CropHealthCostCents = request.CropHealthCostCents
            };

            await _dbContext.PlatformAiSettings.ReplaceOneAsync(
                x => x.Id == 1,
                entity,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            return true;
        }
    }
}
