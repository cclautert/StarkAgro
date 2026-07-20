using StarkAgroAPI.Domain.Commands.Responses.Admin;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
{
    public class GetAllUsersRequest : IRequest<List<AdminUserResponse>>
    {
    }
}
