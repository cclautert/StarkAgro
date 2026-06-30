using AgripeWebAPI.Domain.Commands.Responses.Admin;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Admin
{
    public class GetPlatformAiSettingsRequest : IRequest<AdminAiSettingsResponse>
    {
    }
}
