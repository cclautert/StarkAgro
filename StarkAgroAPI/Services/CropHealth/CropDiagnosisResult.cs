namespace StarkAgroAPI.Services.CropHealth
{
    public class CropDiagnosisInput
    {
        public byte[] ImageBytes { get; set; } = [];
        public string ContentType { get; set; } = "image/jpeg";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime CapturedAt { get; set; }
    }

    public class CropDiseaseSuggestion
    {
        public string Name { get; set; } = string.Empty;
        public string? ScientificName { get; set; }
        public double Probability { get; set; }
        public string? Severity { get; set; }
        public string? Symptoms { get; set; }
        public List<string> Treatments { get; set; } = [];
    }

    public class CropDiagnosisResult
    {
        /// <summary>Falso quando a foto não é de uma planta (parede, chão, dedo na lente).</summary>
        public bool IsPlant { get; set; }

        public string? CropName { get; set; }

        public List<CropDiseaseSuggestion> Diseases { get; set; } = [];

        /// <summary>Resposta crua do provedor, guardada para auditoria de um documento que será assinado.</summary>
        public string? RawJson { get; set; }

        public double TopProbability => Diseases.Count == 0 ? 0 : Diseases.Max(d => d.Probability);
    }
}
