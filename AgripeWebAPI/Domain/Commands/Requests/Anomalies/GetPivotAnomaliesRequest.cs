using AgripeWebAPI.Domain.Commands.Responses.Anomalies;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Anomalies
{
    public class GetPivotAnomaliesRequest : IRequest<List<SensorAnomalyResponse>>
    {
        public int PivotId { get; set; }
        public int UserId { get; set; }
        public bool? AcknowledgedOnly { get; set; }
        public int PageSize { get; set; } = 20;
        public int PageIndex { get; set; } = 0;
    }
}
