using AgripeWebAPI.Domain.Commands.Requests;
using AgripeWebAPI.Domain.Commands.Responses;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers
{
    public class CreateSensorHandler : IRequestHandler<CreateSensorRequest, CreateSensorResponse>
    {
        public Task<CreateSensorResponse> Handle(CreateSensorRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
