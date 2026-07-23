namespace StarkAgroAPI.Models.Entities
{
    public class PlatformAiSettings : Entity
    {
        public string? OpenAiKey { get; set; }
        public string? OpenAiModel { get; set; } = "gpt-4o";
        public string? AnthropicKey { get; set; }
        public string? AnthropicModel { get; set; } = "claude-sonnet-4-6";
        public string? GeminiKey { get; set; }
        public string? GeminiModel { get; set; } = "gemini-1.5-flash";
        public string ActiveProvider { get; set; } = "gemini";

        /// <summary>Chave do classificador de doenças (crop.health / Kindwise). Cobra por foto.</summary>
        public string? CropHealthKey { get; set; }

        /// <summary>
        /// Kill-switch do laudo fitossanitário. Desligado, nenhuma foto vira chamada paga —
        /// o admin corta o custo sem redeploy.
        /// </summary>
        public bool CropHealthEnabled { get; set; } = false;

        /// <summary>
        /// Cota padrão de laudos por mês, para produtores sem cota própria.
        /// <c>0</c> = ilimitado (o comportamento de antes desta configuração existir).
        /// </summary>
        public int DefaultDiagnosisQuotaPerMonth { get; set; } = 0;

        /// <summary>
        /// Custo de uma análise do classificador (crop.health / Kindwise), em <b>centavos</b>.
        /// <para>
        /// Guardado em centavos inteiros de propósito — dinheiro em <c>double</c> acumula erro, e
        /// o projeto já tropeçou em cultura/decimal. O Kindwise cobra ~€0,01–0,05 por foto; o
        /// padrão 3 (€0,03) fica no meio da faixa. O admin ajusta conforme o plano contratado.
        /// </para>
        /// </summary>
        public int CropHealthCostCents { get; set; } = 3;

        // ── NDVI Sentinel-2 (Copernicus Data Space Ecosystem) ──

        /// <summary>Client id do OAuth2 client-credentials da CDSE.</summary>
        public string? CdseClientId { get; set; }

        /// <summary>Client secret do OAuth2 client-credentials da CDSE.</summary>
        public string? CdseClientSecret { get; set; }

        /// <summary>
        /// Kill-switch do NDVI. Desligado, o worker não busca nada da CDSE — o admin corta o
        /// custo de Processing Units sem redeploy.
        /// </summary>
        public bool Sentinel2Enabled { get; set; } = false;

        /// <summary>
        /// Liga NDRE + NDMI na mesma passagem do NDVI (6 bandas de entrada → fator PU <b>2,0</b>
        /// contra 1,33). Desligado, a requisição volta a pedir só as 4 bandas do NDVI. É um eixo
        /// de custo à parte do <see cref="Sentinel2Enabled"/> (que corta tudo): índices extras
        /// custam mais por passagem, então o admin deve subir o <see cref="NdviCostCents"/> ao
        /// ligar — o custo é proxy e não escala sozinho.
        /// </summary>
        public bool ExtraIndicesEnabled { get; set; } = false;

        /// <summary>Custo de uma busca NDVI (Processing Units), em <b>centavos</b> — congelado por reading.</summary>
        public int NdviCostCents { get; set; } = 1;

        /// <summary>
        /// Teto mensal de custo NDVI (PU), em <b>centavos</b>. Batido o teto, o <c>NdviProcessor</c>
        /// para de enfileirar buscas até o mês virar ou o admin subir o valor. <c>0</c> = ilimitado.
        /// </summary>
        public int NdviMonthlyBudgetCents { get; set; } = 0;

        /// <summary>
        /// Teto de áreas monitoradas por usuário. <c>0</c> = ilimitado (comportamento de antes desta
        /// configuração existir). O admin liga sem redeploy.
        /// </summary>
        public int NdviMaxAreasPerUser { get; set; } = 0;

        // ── Fire Shield (NASA FIRMS) ──

        /// <summary>MAP_KEY do FIRMS (gratuito). Sem ela o worker de fogo não busca nada.</summary>
        public string? FirmsMapKey { get; set; }

        /// <summary>
        /// Kill-switch do alerta de foco de calor. Desligado, o <c>FireWatchProcessor</c> não faz
        /// nenhuma chamada externa. Custo é <b>zero</b> (FIRMS é gratuito) — é um freio de ruído/
        /// rate-limit, não de dinheiro.
        /// </summary>
        public bool FireAlertsEnabled { get; set; } = false;

        /// <summary>
        /// Raio (km) ao redor da área em que um foco vira alerta. Default 10; o deck oferece 0,5–20.
        /// </summary>
        public int FireAlertRadiusKm { get; set; } = 10;

        // ── Alertas climáticos (geada / calor extremo) ──

        /// <summary>Kill-switch dos alertas de geada/calor. Desligado, o worker não busca previsão.</summary>
        public bool ClimateAlertsEnabled { get; set; } = false;

        /// <summary>Limiar de geada (°C): temperatura mínima prevista &lt;= este valor dispara alerta. Default 3.</summary>
        public int FrostAlertTempC { get; set; } = 3;

        /// <summary>Limiar de calor extremo (°C): temperatura máxima prevista &gt;= este valor dispara alerta. Default 35.</summary>
        public int HeatAlertTempC { get; set; } = 35;
    }
}
