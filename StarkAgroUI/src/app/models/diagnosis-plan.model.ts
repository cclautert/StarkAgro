export interface DiagnosisPlan {
  id: number;
  name: string;
  /** Mensalidade em centavos (dinheiro em inteiro, sem float). */
  monthlyPriceCents: number;
  includedReportsPerMonth: number;
  /** Preço de cada laudo além do incluso, em centavos. */
  overagePriceCents: number;
  /** Produtores inclusos na mensalidade quando o plano é vendido a uma revenda. */
  includedMembers: number;
  /** Preço de cada produtor além do incluso, em centavos. */
  memberOveragePriceCents: number;
  /** Teto de produtores da revenda; 0 = ilimitado. */
  maxMembers: number;
  active: boolean;
}
