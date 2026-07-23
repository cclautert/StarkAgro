namespace StarkAgroAPI.Domain.Commands.Responses.Admin
{
    public class ZoneDoseResponse
    {
        public string ClassKey { get; set; } = string.Empty;
        public double NitrogenKgHa { get; set; }
        public double PhosphorusKgHa { get; set; }
        public double PotassiumKgHa { get; set; }
    }

    public class FertilizationProfileResponse
    {
        public int Id { get; set; }
        public string Culture { get; set; } = string.Empty;
        public List<ZoneDoseResponse> Doses { get; set; } = [];
    }
}
