using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class DeleteSensorHandler : IRequestHandler<DeleteSensorRequest, DeleteSensorResponse>
    {
        private readonly agpDBContext _dbContext;
        public DeleteSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public Task<DeleteSensorResponse> Handle(DeleteSensorRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
