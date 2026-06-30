using AgripeWebAPI.Domain.Commands.Responses.Admin;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Admin
{
    public class GetAllUsersRequest : IRequest<List<AdminUserResponse>>
    {
    }
}
