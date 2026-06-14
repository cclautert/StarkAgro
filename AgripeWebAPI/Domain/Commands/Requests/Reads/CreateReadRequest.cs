using AgripeWebAPI.Domain.Commands.Responses.Reads;
using MediatR;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AgripeWebAPI.Domain.Commands.Requests.Reads
{
    public class CreateReadRequest : IRequest<CreateReadResponse>
    {
        public int? UserId { get; set; }
        public string Code { get; set; }
        public decimal Value { get; set; }    // firmware legacy — mapped to Humidity on save
        public decimal? Humidity { get; set; }
        public bool IsEdgeAnomaly { get; set; }
        public EdgeStats? EdgeStats { get; set; }

        /// <summary>
        /// Optional idempotency key for deduplication of offline/manual readings (e.g. deviceId + localTimestamp).
        /// When provided, a repeated submission with the same key returns the existing record without creating a duplicate.
        /// Required when submitting manual readings from mobile clients; omitted by automated IoT pipeline.
        /// </summary>
        [Description("Chave de idempotência para evitar duplicação em leituras manuais offline (ex: deviceId + localTimestamp).")]
        [StringLength(256)]
        public string? IdempotencyKey { get; set; }
    }

    public class EdgeStats
    {
        public decimal? Mean { get; set; }
        public decimal? StdDev { get; set; }
        public int? WindowSize { get; set; }
    }
}
