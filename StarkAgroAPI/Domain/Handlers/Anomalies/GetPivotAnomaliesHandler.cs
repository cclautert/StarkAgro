using StarkAgroAPI.Domain.Commands.Requests.Anomalies;
using StarkAgroAPI.Domain.Commands.Responses.Anomalies;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Anomalies
{
    public class GetPivotAnomaliesHandler : IRequestHandler<GetPivotAnomaliesRequest, List<SensorAnomalyResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public GetPivotAnomaliesHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<List<SensorAnomalyResponse>> Handle(GetPivotAnomaliesRequest request, CancellationToken cancellationToken)
        {
            if (request.PivotId <= 0)
            {
                _notifier.Handle(new Notification("PivotId is required."));
                return [];
            }

            var pivot = await _dbContext.Pivots
                .Find(p => p.Id == request.PivotId && p.UserId == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (pivot is null)
            {
                _notifier.Handle(new Notification("Pivot not found."));
                return [];
            }

            var sensors = await _dbContext.Sensors
                .Find(s => s.PivoId == request.PivotId && s.UserId == request.UserId)
                .ToListAsync(cancellationToken);

            if (sensors.Count == 0)
                return [];

            var sensorIds = sensors.Select(s => s.Id).ToList();

            var filterDefs = new List<FilterDefinition<SensorAnomaly>>
            {
                Builders<SensorAnomaly>.Filter.In(a => a.SensorId, sensorIds),
                Builders<SensorAnomaly>.Filter.Eq(a => a.UserId, request.UserId)
            };

            if (request.AcknowledgedOnly.HasValue)
                filterDefs.Add(Builders<SensorAnomaly>.Filter.Eq(a => a.Acknowledged, request.AcknowledgedOnly.Value));

            var filter = Builders<SensorAnomaly>.Filter.And(filterDefs);

            var anomalies = await _dbContext.SensorAnomalies
                .Find(filter)
                .SortByDescending(a => a.Date)
                .Skip(request.PageIndex * request.PageSize)
                .Limit(request.PageSize)
                .ToListAsync(cancellationToken);

            return anomalies.Select(a => new SensorAnomalyResponse
            {
                Id = a.Id,
                SensorId = a.SensorId,
                PivotId = a.PivotId,
                ReadSensorId = a.ReadSensorId,
                Value = a.Value,
                ExpectedMin = a.ExpectedMin,
                ExpectedMax = a.ExpectedMax,
                Date = a.Date,
                Acknowledged = a.Acknowledged
            }).ToList();
        }
    }
}
