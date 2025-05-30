using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Reads
{
    public class CreateReadHandler : IRequestHandler<CreateReadRequest, CreateReadResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreateReadHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<CreateReadResponse> Handle(CreateReadRequest request, CancellationToken cancellationToken)
        {
            _dbContext.ReadSensors.Add(new ReadSensor { SensorId = GetIdByCode(request.Code), Value = request.Value, Date = DateTime.Now });
            _dbContext.SaveChanges();

            return Task.FromResult(new CreateReadResponse());
        }

        private int GetIdByCode(string code)
        {
            var sensor = _dbContext.Sensors.FirstOrDefault(s => s.Code == code) ?? throw new ArgumentException("Sensor not found with the provided code.");
            return sensor.Id;
        }
    }
}
