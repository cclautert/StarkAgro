using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

            var quadranteDataMap = await _dbContext.ReadSensors
            .Where(r => r.Sensor.PivoId == request.PivotId && r.Date >= startDate)
            .GroupBy(r => r.Sensor.Quadrante) // Agrupa pelo 'int' do quadrante
            .Select(group => new
            {
                NumeroQuadrante = group.Key,
                // É importante garantir que a divisão seja de ponto flutuante.
                // Se 'r.Value' for int, converta para double ou decimal.
                Media = group.Average(r => (double)r.Value) 
            })
            .ToDictionaryAsync(
                // A chave do dicionário é o número do quadrante
                keySelector: result => result.NumeroQuadrante, 
                // O valor é uma tupla com a média e a cor calculada
                elementSelector: result => (
                    Average: (decimal)result.Media, // Converte a média para decimal
                    Color: (result.Media < 15) ? "#2196F3" :    //Azul
                           (result.Media < 40) ? "#4CAF50" :    //verde
                           (result.Media < 65) ? "#FFC107" :    //Amarelo
                           "#F44336"    //Vermelho
                )
            );

            // Opcional: buscar o nome do pivô
            var pivotName = await _dbContext.Pivots
                                            .Where(p => p.Id == request.PivotId)
                                            .Select(p => p.Name)
                                            .FirstOrDefaultAsync();

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
