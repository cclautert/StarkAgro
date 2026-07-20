using StarkAgroAPI.Domain.Commands.Responses.Admin;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
{
    public class AdminToggleUserActiveRequest : IRequest<AdminUserResponse>
    {
        public int Id { get; set; }
        public bool Active { get; set; }
    }
}
