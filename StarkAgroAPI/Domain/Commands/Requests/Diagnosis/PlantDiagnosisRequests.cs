using StarkAgroAPI.Domain.Commands.Responses.Diagnosis;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Diagnosis
{
    /// <summary>
    /// Cria um laudo a partir da foto enviada pelo produtor.
    /// O <c>IFormFile</c> fica no controller: aqui chega só o conteúdo, para o handler
    /// continuar testável sem tipos de HTTP.
    /// </summary>
    public class CreatePlantDiagnosisRequest : IRequest<CreatePlantDiagnosisResponse?>
    {
        public byte[] ImageBytes { get; set; } = [];
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;

        public int? PivotId { get; set; }
        public string? CropName { get; set; }
        public string? Notes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class GetPlantDiagnosisListRequest : IRequest<List<PlantDiagnosisSummaryResponse>>
    {
        public string? Status { get; set; }
        public int PageSize { get; set; } = 20;
        public int PageIndex { get; set; } = 0;
    }

    public class GetPlantDiagnosisByIdRequest : IRequest<PlantDiagnosisResponse?>
    {
        public int Id { get; set; }
    }

    public class GetPlantDiagnosisStatusRequest : IRequest<PlantDiagnosisStatusResponse?>
    {
        public int Id { get; set; }
    }

    public class GetPlantDiagnosisImageRequest : IRequest<PlantDiagnosisImageResponse?>
    {
        public int Id { get; set; }
    }

    public class DeletePlantDiagnosisRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }
}
