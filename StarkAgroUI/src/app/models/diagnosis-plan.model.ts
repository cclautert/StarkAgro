export interface DiagnosisPlan {
  id: number;
  name: string;
  /** Mensalidade em centavos (dinheiro em inteiro, sem float). */
  monthlyPriceCents: number;
  includedReportsPerMonth: number;
  /** Preço de cada laudo além do incluso, em centavos. */
  overagePriceCents: number;
  active: boolean;
}
