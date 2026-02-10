using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;
using System.Linq;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
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

            var sensors = await _dbContext.Sensors
                .Find(s => s.PivoId == request.PivotId)
                .Project(s => new { s.Id, s.Quadrante })
                .ToListAsync(cancellationToken);

            var sensorsById = sensors.ToDictionary(s => s.Id, s => s.Quadrante);
            var sensorIds = sensorsById.Keys.ToList();

            var reads = sensorIds.Count == 0
                ? new List<Models.Entities.ReadSensor>()
                : await _dbContext.ReadSensors
                    .Find(r => sensorIds.Contains(r.SensorId) && r.Date >= startDate)
                    .ToListAsync(cancellationToken);

            var quadranteDataMap = reads
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
                        Color: (result.Media < 15) ? "#2196F3" :
                               (result.Media < 40) ? "#4CAF50" :
                               (result.Media < 65) ? "#FFC107" :
                               "#F44336"
                    )
                );

            // Opcional: buscar o nome do pivô
            var pivotName = await _dbContext.Pivots
                .Find(p => p.Id == request.PivotId)
                .Project(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);

            // --- Etapa 2: Construir o objeto de resposta final ---
            var response = new GetReadByPivotIdResponse
            {
                Id = request.PivotId,
                Name = pivotName,
                Quadrante = new Quadrante() // Inicializa o objeto Quadrante
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

            return response;
        }
    }
}
