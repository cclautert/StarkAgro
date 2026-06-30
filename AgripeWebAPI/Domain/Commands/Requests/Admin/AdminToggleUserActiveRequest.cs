using AgripeWebAPI.Domain.Commands.Responses.Admin;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Admin
{
    public class AdminToggleUserActiveRequest : IRequest<AdminUserResponse>
    {
        public int Id { get; set; }
        public bool Active { get; set; }
    }
}
