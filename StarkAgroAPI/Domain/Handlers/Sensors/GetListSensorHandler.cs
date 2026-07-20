using StarkAgroAPI.Domain.Commands.Requests.Sensors;
using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Sensors
{
    public class GetListSensorHandler : IRequestHandler<GetListSensorByUserIdRequest, IList<GetSensorResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetListSensorHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }
        
        public async Task<IList<GetSensorResponse>> Handle(GetListSensorByUserIdRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to list sensors.");

            var sensors = await _dbContext.Sensors
                .Find(x => x.UserId == userId)
                .ToListAsync(cancellationToken);

            var pivotIds = sensors.Select(x => x.PivoId).Distinct().ToList();
            var pivots = pivotIds.Count == 0
                ? new List<Pivot>()
                : await _dbContext.Pivots.Find(x => pivotIds.Contains(x.Id)).ToListAsync(cancellationToken);

            var pivotsById = pivots.ToDictionary(x => x.Id, x => x);

            return sensors.Select(sensor => new GetSensorResponse
            {
                Id = sensor.Id,
                Name = sensor.Name,
                Code = sensor.Code,
                Pivot = pivotsById.TryGetValue(sensor.PivoId, out var pivot) ? pivot : new Pivot { Id = sensor.PivoId },
                Quadrante = sensor.Quadrante
            }).ToList();
        }
    }
}
