using StarkAgroAPI.Domain.Commands.Requests.Sensors;
using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Sensors
{
    public class SendSensorDownlinkHandler : IRequestHandler<SendSensorDownlinkRequest, SendSensorDownlinkResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly ILoRaWanDownlinkService _downlinkService;

        public SendSensorDownlinkHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            ILoRaWanDownlinkService downlinkService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _downlinkService = downlinkService ?? throw new ArgumentNullException(nameof(downlinkService));
        }

        public async Task<SendSensorDownlinkResponse> Handle(
            SendSensorDownlinkRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user required.");

            var sensor = await _dbContext.Sensors
                .Find(x => x.Id == request.SensorId && x.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sensor == null)
                return new SendSensorDownlinkResponse { Success = false, Message = "Sensor não encontrado." };

            if (string.IsNullOrEmpty(sensor.Code))
                return new SendSensorDownlinkResponse { Success = false, Message = "Sensor sem código (DevEUI)." };

            var user = await _dbContext.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);

            var effectiveInterval = sensor.UplinkIntervalSeconds ?? user?.UplinkIntervalSeconds ?? 10800;

            if (effectiveInterval <= 0)
                return new SendSensorDownlinkResponse { Success = false, Message = "Intervalo de leitura inválido. Configure nas propriedades do sensor ou nas configurações globais." };

            var sent = await _downlinkService.SendUplinkIntervalAsync(
                sensor.Code,
                effectiveInterval,
                cancellationToken);

            return sent
                ? new SendSensorDownlinkResponse { Success = true, Message = "Downlink enfileirado com sucesso. O sensor aplicará a configuração na próxima transmissão." }
                : new SendSensorDownlinkResponse { Success = false, Message = "Falha ao publicar downlink no broker MQTT. Verifique a configuração do servidor." };
        }
    }
}
