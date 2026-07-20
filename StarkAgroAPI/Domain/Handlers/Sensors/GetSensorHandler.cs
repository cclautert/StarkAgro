using StarkAgroAPI.Domain.Commands.Requests.Sensors;
using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Sensors
{
    public class GetSensorHandler : IRequestHandler<GetSensorRequest, GetSensorResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetSensorHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<GetSensorResponse?> Handle(GetSensorRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user required.");

            var sensor = await _dbContext.Sensors
                .Find(x => x.Id == request.Id && x.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sensor == null)
            {
                return null;
            }

            var pivot = await _dbContext.Pivots
                .Find(x => x.Id == sensor.PivoId)
                .FirstOrDefaultAsync(cancellationToken) ?? new Pivot { Id = sensor.PivoId };

            return new GetSensorResponse
            {
                Id = sensor.Id,
                Name = sensor.Name,
                Code = sensor.Code,
                Pivot = pivot,
                Quadrante = sensor.Quadrante,
                UplinkIntervalSeconds = sensor.UplinkIntervalSeconds
            };
        }
    }
}
