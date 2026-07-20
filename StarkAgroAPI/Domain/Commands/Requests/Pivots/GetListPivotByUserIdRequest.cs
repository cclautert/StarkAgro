using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;
using System.Collections.Generic;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class GetListPivotByUserIdRequest : IRequest<List<GetPivotResponse>>
    {
        public int? UserId { get; set; }
    }
}
