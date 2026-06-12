using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;
using System.Linq;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public sealed record SensorSummary(int Id, int Quadrante);

    public class GetReadByPivotIdHandler : IRequestHandler<GetListReadByPivotIdRequest, GetReadByPivotIdResponse>
    {
        private readonly agpDBContext _dbContext;

        // Constructor updated to inject IReadSensor dependency
        public GetReadByPivotIdHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<GetReadByPivotIdResponse> Handle(GetListReadByPivotIdRequest request, CancellationToken cancellationToken)
        {
            var startDate = DateTime.UtcNow.AddDays(-request.NumberOfReads);

            var humidityFilter = Builders<Sensor>.Filter.And(
                Builders<Sensor>.Filter.Eq(s => s.PivoId, request.PivotId),
                Builders<Sensor>.Filter.Eq(s => s.UserId, request.UserId),
                Builders<Sensor>.Filter.Or(
                    Builders<Sensor>.Filter.Eq(s => s.MetricType, MetricType.Humidity),
                    Builders<Sensor>.Filter.Exists(nameof(Sensor.MetricType), false)
                )
            );
            var sensors = await _dbContext.Sensors
                .Find(humidityFilter)
                .Project(s => new SensorSummary(s.Id, s.Quadrante))
                .ToListAsync(cancellationToken);

            var sensorsById = sensors.ToDictionary(s => s.Id, s => s.Quadrante);
            var sensorIds = sensorsById.Keys.ToList();

            var reads = sensorIds.Count == 0
                ? new List<Models.Entities.ReadSensor>()
                : await _dbContext.ReadSensors
                    .Find(r => sensorIds.Contains(r.SensorId) && r.Date >= startDate)
                    .ToListAsync(cancellationToken);

            var readsByQuadrant = reads
                .Where(r => sensorsById.ContainsKey(r.SensorId))
                .GroupBy(r => sensorsById[r.SensorId])
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(r => r.Date)
                          .Select(r => new ReadEntry { Value = r.Value, Date = r.Date })
                          .ToList()
                );

            // Buscar dados do pivô (nome e limites) e do usuário (limites padrão)
            var pivot = await _dbContext.Pivots
                .Find(p => p.Id == request.PivotId)
                .FirstOrDefaultAsync(cancellationToken);

            var user = await _dbContext.Users
                .Find(u => u.Id == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            // Resolver limites com hierarquia: pivô > usuário > padrão
            var limiteInferior = (double)(pivot?.LimiteInferior ?? user?.LimiteInferior ?? 25m);
            var limiteSuperior = (double)(pivot?.LimiteSuperior ?? user?.LimiteSuperior ?? 75m);

            // Para a média do quadrante: usar apenas a leitura mais recente de cada sensor
            var latestReads = new List<ReadSensor>();
            foreach (var sensorId in sensorIds)
            {
                var latest = await _dbContext.ReadSensors
                    .Find(r => r.SensorId == sensorId)
                    .SortByDescending(r => r.Date)
                    .Limit(1)
                    .FirstOrDefaultAsync(cancellationToken);
                if (latest != null) latestReads.Add(latest);
            }

            var quadranteDataMap = latestReads
                .Where(read => sensorsById.ContainsKey(read.SensorId))
                .GroupBy(read => sensorsById[read.SensorId])
                .Select(group => new
                {
                    NumeroQuadrante = group.Key,
                    Media = group.Average(read => (double)read.Value)
                })
                .ToDictionary(
                    result => result.NumeroQuadrante,
                    result => (
                        Average: (decimal)result.Media,
                        Color: (result.Media < limiteInferior) ? "#F44336" :
                               (result.Media > limiteSuperior) ? "#2196F3" :
                               "#4CAF50"
                    )
                );

            // --- Etapa 2: Construir o objeto de resposta final ---
            var response = new GetReadByPivotIdResponse
            {
                Id = request.PivotId,
                Name = pivot?.Name,
                LimiteInferior = pivot?.LimiteInferior ?? user?.LimiteInferior ?? 25m,
                LimiteSuperior = pivot?.LimiteSuperior ?? user?.LimiteSuperior ?? 75m,
                Quadrante = new Quadrante()
            };

            // Agora, populamos o objeto Quadrante usando o mapa.
            // Se o quadrante não estiver no mapa, a cor será "Cinza" e a média será null.

            // Quadrante 1 (TopRight)
            if (quadranteDataMap.TryGetValue(1, out var data1))
            {
                response.Quadrante.TopRight = data1.Color;
                response.Quadrante.TopRightAvg = data1.Average;
            }
            else
            {
                response.Quadrante.TopRight = "#607D8B";
            }
            response.Quadrante.TopRightReads = readsByQuadrant.GetValueOrDefault(1) ?? new();

            // Quadrante 2 (BottomRight)
            if (quadranteDataMap.TryGetValue(2, out var data2))
            {
                response.Quadrante.BottomRight = data2.Color;
                response.Quadrante.BottomRightAvg = data2.Average;
            }
            else
            {
                response.Quadrante.BottomRight = "#607D8B";
            }
            response.Quadrante.BottomRightReads = readsByQuadrant.GetValueOrDefault(2) ?? new();

            // Quadrante 3 (BottomLeft)
            if (quadranteDataMap.TryGetValue(3, out var data3))
            {
                response.Quadrante.BottomLeft = data3.Color;
                response.Quadrante.BottomLeftAvg = data3.Average;
            }
            else
            {
                response.Quadrante.BottomLeft = "#607D8B";
            }
            response.Quadrante.BottomLeftReads = readsByQuadrant.GetValueOrDefault(3) ?? new();

            // Quadrante 4 (TopLeft)
            if (quadranteDataMap.TryGetValue(4, out var data4))
            {
                response.Quadrante.TopLeft = data4.Color;
                response.Quadrante.TopLeftAvg = data4.Average;
            }
            else
            {
                response.Quadrante.TopLeft = "#607D8B";
            }
            response.Quadrante.TopLeftReads = readsByQuadrant.GetValueOrDefault(4) ?? new();

            return response;
        }
    }
}
