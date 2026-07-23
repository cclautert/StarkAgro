using StarkAgroAPI.Domain.Commands.Responses.Admin;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
{
    /// <summary>Uma dose de NPK (kg/ha) para uma classe de biomassa.</summary>
    public class ZoneDoseInput
    {
        [Required]
        public string ClassKey { get; set; } = string.Empty;
        public double NitrogenKgHa { get; set; }
        public double PhosphorusKgHa { get; set; }
        public double PotassiumKgHa { get; set; }
    }

    public class GetFertilizationProfilesRequest : IRequest<List<FertilizationProfileResponse>>
    {
    }

    public class CreateFertilizationProfileRequest : IRequest<FertilizationProfileResponse>
    {
        [Required]
        public string Culture { get; set; } = string.Empty;
        public List<ZoneDoseInput> Doses { get; set; } = [];
    }

    public class UpdateFertilizationProfileRequest : IRequest<FertilizationProfileResponse>
    {
        public int Id { get; set; }
        [Required]
        public string Culture { get; set; } = string.Empty;
        public List<ZoneDoseInput> Doses { get; set; } = [];
    }

    public class DeleteFertilizationProfileRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }
}
