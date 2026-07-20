using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
{
    public class AdminDeleteUserRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }
}
