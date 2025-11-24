using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;
using System.Collections.Generic;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class GetListPivotByUserIdRequest : IRequest<List<GetPivotResponse>>
    {
        public int? UserId { get; set; }
    }
}
