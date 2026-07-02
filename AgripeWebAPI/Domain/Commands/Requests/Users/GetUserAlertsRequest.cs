using AgripeWebAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class GetUserAlertsRequest : IRequest<IList<UserAlertResponse>>
    {
    }
}
