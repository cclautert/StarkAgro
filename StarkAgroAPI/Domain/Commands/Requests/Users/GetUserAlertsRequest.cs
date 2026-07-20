using StarkAgroAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Users
{
    public class GetUserAlertsRequest : IRequest<IList<UserAlertResponse>>
    {
    }
}
