using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Ndvi
{
    public class ListMonitoredAreasRequest : IRequest<List<MonitoredAreaResponse>>
    {
    }

    public class GetMonitoredAreaRequest : IRequest<MonitoredAreaResponse?>
    {
        public int Id { get; set; }
    }

    public class GetNdviTrendRequest : IRequest<Responses.Ndvi.NdviTrendResponse?>
    {
        public int AreaId { get; set; }
    }

    public class GetNdviOverlayImageRequest : IRequest<NdviOverlayImageResponse?>
    {
        public int AreaId { get; set; }
        public int ReadingId { get; set; }
    }

    public class DeleteMonitoredAreaRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }

    public class CreateMonitoredAreaRequest : IRequest<MonitoredAreaResponse?>
    {
        [Required]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "O nome da área deve ter entre 1 e 120 caracteres.")]
        public string Name { get; set; } = string.Empty;

        public string? Crop { get; set; }

        /// <summary>"Circle" ou "Polygon".</summary>
        [Required]
        public string AreaKind { get; set; } = string.Empty;

        // Round-trip do círculo (opcional; a geometria autoritativa vem sempre no Ring).
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public double? RadiusM { get; set; }
        public double? Altitude { get; set; }
        public string? LocationAddress { get; set; }

        public bool MonitoringEnabled { get; set; } = true;

        /// <summary>Anel do polígono (lat/lng). O servidor fecha e converte para [lng,lat].</summary>
        [Required]
        public List<GeoCoordinate> Ring { get; set; } = [];
    }

    public class EditMonitoredAreaRequest : IRequest<MonitoredAreaResponse?>
    {
        public int Id { get; set; }

        [Required]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "O nome da área deve ter entre 1 e 120 caracteres.")]
        public string Name { get; set; } = string.Empty;

        public string? Crop { get; set; }

        [Required]
        public string AreaKind { get; set; } = string.Empty;

        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public double? RadiusM { get; set; }
        public double? Altitude { get; set; }
        public string? LocationAddress { get; set; }

        public bool MonitoringEnabled { get; set; } = true;

        [Required]
        public List<GeoCoordinate> Ring { get; set; } = [];
    }
}
