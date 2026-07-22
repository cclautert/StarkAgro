export interface PlatformAiSettings {
  openAiKey?: string | null;
  openAiModel?: string | null;
  anthropicKey?: string | null;
  anthropicModel?: string | null;
  geminiKey?: string | null;
  geminiModel?: string | null;
  activeProvider: 'openai' | 'anthropic' | 'gemini';

  /** Classificador de doenças de planta (crop.health / Kindwise) — cobra por foto. */
  cropHealthKey?: string | null;
  cropHealthEnabled?: boolean;

  /** Cota padrão de laudos/mês. 0 = ilimitado. */
  defaultDiagnosisQuotaPerMonth?: number;

  /** Custo por análise do classificador, em centavos (dinheiro em inteiro, sem float). */
  cropHealthCostCents?: number;

  /** Custo de IA já gasto no mês corrente, em centavos (só leitura). */
  readonly currentMonthAiCostCents?: number;

  // ── NDVI Sentinel-2 (Copernicus Data Space Ecosystem) ──

  /** Credenciais OAuth2 client-credentials da CDSE. */
  cdseClientId?: string | null;
  cdseClientSecret?: string | null;

  /** Kill-switch do NDVI: desligado, o worker não busca nada da CDSE. */
  sentinel2Enabled?: boolean;
  extraIndicesEnabled?: boolean;

  /** Custo de uma busca NDVI (Processing Units), em centavos — congelado por leitura. */
  ndviCostCents?: number;

  /** Teto mensal de custo NDVI (PU), em centavos. 0 = ilimitado. */
  ndviMonthlyBudgetCents?: number;

  /** Teto de áreas monitoradas por usuário. 0 = ilimitado. */
  ndviMaxAreasPerUser?: number;

  /** Custo NDVI já gasto no mês corrente, em centavos (só leitura). */
  readonly currentMonthNdviCostCents?: number;
}
