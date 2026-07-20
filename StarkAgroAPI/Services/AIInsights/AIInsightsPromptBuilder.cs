using System.Globalization;
using System.Text;

namespace StarkAgroAPI.Services.AIInsights
{
    internal static class AIInsightsPromptBuilder
    {
        internal const string SystemPrompt =
            "Você é um agrônomo especialista em irrigação por pivô central. " +
            "Analise os dados fornecidos e gere uma recomendação prática e objetiva em português do Brasil. " +
            "Seja direto, use linguagem acessível ao agricultor. Máximo de 4 parágrafos.";

        internal static string BuildUserMessage(PivotAIContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## Pivot: {context.PivotName}");
            sb.AppendLine($"- Limite inferior de umidade: {context.LimiteInferior}%");
            sb.AppendLine($"- Limite superior de umidade: {context.LimiteSuperior}%");
            if (context.Latitude.HasValue && context.Longitude.HasValue)
            {
                sb.AppendLine($"- Localização: {context.Latitude.Value.ToString("F4", CultureInfo.InvariantCulture)}, {context.Longitude.Value.ToString("F4", CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine();
            sb.AppendLine("## Leituras de Sensores (últimas 48h)");
            foreach (var sensor in context.SensorReadings)
            {
                sb.AppendLine($"### Sensor {sensor.SensorCode ?? "?"} — Quadrante {sensor.Quadrante}");
                if (sensor.Readings.Count == 0)
                {
                    sb.AppendLine("  Sem leituras.");
                }
                else
                {
                    foreach (var r in sensor.Readings)
                        sb.AppendLine($"  {r.Date:dd/MM HH:mm} → {r.Value:0.0}%");
                }
            }

            if (!string.IsNullOrEmpty(context.ForecastSummary))
            {
                sb.AppendLine();
                sb.AppendLine("## Previsão do Tempo (7 dias)");
                sb.AppendLine(context.ForecastSummary);
            }

            if (context.RecentAnomalies.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Anomalias Recentes");
                foreach (var a in context.RecentAnomalies)
                    sb.AppendLine($"  {a.Date:dd/MM HH:mm} — Sensor {a.SensorId}: valor {a.Value:0.0}% (esperado: {a.ExpectedMin:0.0}–{a.ExpectedMax:0.0}%)");
            }

            sb.AppendLine();
            sb.AppendLine("Com base nesses dados, forneça uma análise do estado atual da irrigação e recomendações práticas para o agricultor.");
            return sb.ToString();
        }
    }
}
