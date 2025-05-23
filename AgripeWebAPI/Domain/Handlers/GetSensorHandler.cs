using AgripeWebAPI.Domain.Commands.Requests;
using AgripeWebAPI.Domain.Commands.Responses;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers
{
    public class GetSensorHandler : IRequestHandler<GetSensorRequest, GetSensorResponse>
    {
        public Task<GetSensorResponse> Handle(GetSensorRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
