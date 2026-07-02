using System.ComponentModel.DataAnnotations;

namespace AgripeWebAPI.Models.Entities
{
    public class User : Entity
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public bool Active { get; set; }
        public decimal LimiteInferior { get; set; } = 25m;
        public decimal LimiteSuperior { get; set; } = 75m;
        public double? RainThresholdMm { get; set; }
        public string? GeminiApiKey { get; set; }
        public string? ExpoPushToken { get; set; }
        public string? WebPushSubscriptionJson { get; set; }
        public List<string> WebPushSubscriptions { get; set; } = new();
        public int? UplinkIntervalSeconds { get; set; } = 10800;
        public bool IsAdmin { get; set; } = false;
        public DateTime? AlertsReadAt { get; set; }
    }
}
