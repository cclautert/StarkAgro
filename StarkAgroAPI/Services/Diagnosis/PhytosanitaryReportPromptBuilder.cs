using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.CropHealth;
using System.Text;

namespace StarkAgroAPI.Services.Diagnosis
{
    /// <summary>
    /// Monta o prompt do laudo fitossanitário. O diagnóstico já vem pronto do classificador
    /// especializado — o LLM aqui só <b>redige</b> e, sobretudo, <b>correlaciona</b> a doença
    /// com a umidade, a irrigação e a chuva prevista daquele pivô. Essa correlação é o produto.
    /// </summary>
    public static class PhytosanitaryReportPromptBuilder
    {
        public const string SystemPrompt = """
            Você é um engenheiro agrônomo redigindo um LAUDO FITOSSANITÁRIO PRELIMINAR em
            português do Brasil, para um produtor rural. O laudo será revisado e assinado por um
            engenheiro agrônomo humano antes de chegar ao produtor.

            O diagnóstico da doença JÁ FOI FEITO por um classificador de imagens especializado.
            Você NÃO decide qual é a doença: você redige o laudo a partir das probabilidades que
            recebeu e as CORRELACIONA com os dados de campo daquela lavoura.

            REGRAS INVIOLÁVEIS:
            1. NUNCA cite produto comercial, marca, princípio ativo ou dose. Prescrever defensivo
               é ato privativo de engenheiro agrônomo (receituário agronômico, com ART). Descreva
               apenas ESTRATÉGIAS DE MANEJO (cultural, preventivo, monitoramento). Se o produtor
               precisar de defensivo, escreva que "a prescrição depende de avaliação do agrônomo
               responsável".
            2. NUNCA afirme um diagnóstico com mais certeza do que a probabilidade informada.
               Use "provável", "compatível com", "sugere" — e cite a probabilidade.
            3. NUNCA invente dados de campo. Use apenas os que estiverem no contexto. Se não houver
               dados de sensor ou de clima, diga explicitamente que o laudo foi feito só com a foto
               e que a correlação com o manejo não pôde ser avaliada.
            4. Escreva para quem trabalha no campo: direto, sem jargão desnecessário, sem enrolação.

            ESTRUTURA (markdown, use exatamente estas seções):
            ## Identificação
            ## Sintomas observados
            ## Diagnóstico provável
            ## Correlação com o manejo
            ## Recomendações de manejo
            ## Limitações deste laudo

            A seção "Correlação com o manejo" é a mais importante: relacione a doença provável com
            a umidade do solo, o histórico de irrigação e a chuva prevista. É o que um aplicativo
            de foto não consegue fazer. Se os dados apontarem para uma condição favorável ao
            patógeno (por exemplo, solo acima do limite superior por vários dias somado a chuva
            prevista), diga isso com todas as letras e aponte o que revisar no manejo.

            Termine SEMPRE com a linha, em itálico:
            _Laudo técnico informativo. Não constitui receituário agronômico nem ART._
            """;

        public static string BuildUserMessage(
            PlantDiagnosis diagnosis,
            CropDiagnosisResult diagnosisResult,
            PlantDiagnosisContextSnapshot snapshot)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# DADOS DO LAUDO");
            sb.AppendLine();
            sb.AppendLine($"Data da foto: {diagnosis.CapturedAt:dd/MM/yyyy HH:mm} (UTC)");
            sb.AppendLine($"Cultura informada pelo produtor: {Or(diagnosis.CropName, "não informada")}");

            if (!string.IsNullOrWhiteSpace(diagnosisResult.CropName))
                sb.AppendLine($"Cultura reconhecida na imagem: {diagnosisResult.CropName}");

            sb.AppendLine($"Observação do produtor: {Or(diagnosis.ProducerNotes, "nenhuma")}");
            sb.AppendLine();

            sb.AppendLine("## Saída do classificador de imagens (não é opinião sua — é o dado)");
            if (diagnosisResult.Diseases.Count == 0)
            {
                sb.AppendLine("Nenhuma doença identificada com confiança.");
            }
            else
            {
                foreach (var disease in diagnosisResult.Diseases.Take(3))
                {
                    sb.AppendLine($"- **{disease.Name}**" +
                                  (string.IsNullOrWhiteSpace(disease.ScientificName) ? "" : $" ({disease.ScientificName})") +
                                  $" — probabilidade {ProbabilityFormatter.ToPercent(disease.Probability)}");

                    if (!string.IsNullOrWhiteSpace(disease.Severity))
                        sb.AppendLine($"  - severidade: {disease.Severity}");
                    if (!string.IsNullOrWhiteSpace(disease.Symptoms))
                        sb.AppendLine($"  - sintomas descritos na base: {Truncate(disease.Symptoms, 600)}");
                    if (disease.Treatments.Count > 0)
                        sb.AppendLine($"  - manejo sugerido pela base: {Truncate(string.Join("; ", disease.Treatments), 600)}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("## Dados de campo desta lavoura");
            if (string.IsNullOrWhiteSpace(snapshot.PivotName))
            {
                sb.AppendLine("O produtor não vinculou a foto a um pivô. **Não há dados de sensor nem de clima**");
                sb.AppendLine("para este laudo — deixe isso claro na seção de limitações e não invente correlação.");
            }
            else
            {
                sb.AppendLine($"Pivô: {snapshot.PivotName}");

                if (snapshot.MoistureAvg7d.HasValue)
                    sb.AppendLine($"Umidade média do solo nos últimos 7 dias: {snapshot.MoistureAvg7d:0.0}%");

                if (snapshot.LimiteInferior.HasValue || snapshot.LimiteSuperior.HasValue)
                    sb.AppendLine($"Faixa ideal configurada: {snapshot.LimiteInferior:0.0}% a {snapshot.LimiteSuperior:0.0}%");

                if (snapshot.DaysAboveUpperLimit > 0)
                    sb.AppendLine($"**Dias (dos últimos 7) com média acima do limite superior: {snapshot.DaysAboveUpperLimit}** " +
                                  "— solo mais úmido do que o desejado, condição que favorece fungos.");

                if (snapshot.LastReadings.Count > 0)
                {
                    sb.AppendLine("Última leitura por sensor:");
                    foreach (var reading in snapshot.LastReadings)
                    {
                        sb.AppendLine($"  - {Or(reading.SensorCode, "sensor")}" +
                                      (reading.Quadrante.HasValue ? $" (quadrante {reading.Quadrante})" : "") +
                                      $": umidade {reading.Humidity:0.0}%" +
                                      (reading.Temperature.HasValue ? $", temperatura {reading.Temperature:0.0} °C" : "") +
                                      $" em {reading.Date:dd/MM HH:mm}");
                    }
                }

                if (snapshot.OpenAnomalies > 0)
                    sb.AppendLine($"Anomalias de sensor em aberto: {snapshot.OpenAnomalies}");

                if (snapshot.IrrigationAlerts7d > 0)
                    sb.AppendLine($"Alertas de irrigação nos últimos 7 dias: {snapshot.IrrigationAlerts7d}");

                sb.AppendLine(string.IsNullOrWhiteSpace(snapshot.ForecastSummary)
                    ? "Previsão do tempo: indisponível para este pivô."
                    : $"Previsão do tempo: {snapshot.ForecastSummary}");
            }

            sb.AppendLine();
            sb.AppendLine("Redija agora o laudo, seguindo exatamente a estrutura de seções definida.");

            return sb.ToString();
        }

        private static string Or(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value[..max] + "...";
    }
}
