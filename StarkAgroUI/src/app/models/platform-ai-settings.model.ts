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
}
