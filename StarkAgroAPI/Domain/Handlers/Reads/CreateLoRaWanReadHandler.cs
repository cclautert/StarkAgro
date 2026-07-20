using StarkAgroAPI.Domain.Commands.Requests.Reads;
using StarkAgroAPI.Domain.Commands.Responses.Reads;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Reads
{
    public class CreateLoRaWanReadHandler : IRequestHandler<CreateLoRaWanReadRequest, CreateReadResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ILogger<CreateLoRaWanReadHandler> _logger;

        public CreateLoRaWanReadHandler(agpDBContext dbContext, ILogger<CreateLoRaWanReadHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CreateReadResponse?> Handle(CreateLoRaWanReadRequest request, CancellationToken cancellationToken)
        {
            var sensor = await _dbContext.Sensors
                .Find(s => s.Code.ToUpper() == request.Code.ToUpper())
                .FirstOrDefaultAsync(cancellationToken);

            if (sensor == null)
            {
                _logger.LogWarning("LoRaWAN read rejected: no sensor registered with code '{Code}'", request.Code);
                return null;
            }

            // fcnt zera no rejoin do device, então a chave inclui o timestamp do uplink:
            // reentrega do broker tem o mesmo time+fcnt (deduplica); um fcnt=0 pós-rejoin
            // tem time diferente (grava, não perde leitura). Sem fcnt/time não há como
            // deduplicar — cai num GUID único (sempre grava).
            var hasIdempotency = request.Fcnt.HasValue && request.ReadAt.HasValue;
            var idempotencyKey = hasIdempotency
                ? $"{request.Code.ToUpperInvariant()}:{request.Fcnt}:{request.ReadAt!.Value.ToUniversalTime().Ticks}"
                : Guid.NewGuid().ToString();

            // Reentrega idêntica do broker MQTT (QoS 1) — no-op idempotente, não é erro.
            if (hasIdempotency)
            {
                var existing = await _dbContext.ReadSensors
                    .Find(r => r.IdempotencyKey == idempotencyKey)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existing != null)
                {
                    _logger.LogInformation(
                        "Uplink LoRaWAN duplicado ignorado para sensor '{Code}' (key '{Key}')",
                        request.Code, idempotencyKey);
                    return null;
                }
            }

            var read = new ReadSensor
            {
                Id = await _dbContext.GetNextIdAsync(nameof(ReadSensor), cancellationToken),
                SensorId = sensor.Id,
                UserId = sensor.UserId,
                Date = request.ReadAt ?? DateTime.UtcNow,
                Humidity = request.Humidity,
                Temperature = request.Temperature,
                BatteryVoltage = request.BatteryVoltage,
                IdempotencyKey = idempotencyKey
            };

            try
            {
                await _dbContext.ReadSensors.InsertOneAsync(read, cancellationToken: cancellationToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Backstop de corrida: dois uplinks idênticos concorrentes passaram o check
                // acima; o índice único garante um só. Trata como no-op, não como erro.
                _logger.LogInformation(
                    "Uplink LoRaWAN duplicado ignorado para sensor '{Code}' (key '{Key}')",
                    request.Code, idempotencyKey);
                return null;
            }

            return new CreateReadResponse
            {
                Id = read.Id,
                SensorId = sensor.Id,
                UserId = sensor.UserId
            };
        }
    }
}
