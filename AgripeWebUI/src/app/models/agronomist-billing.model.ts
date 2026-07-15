export interface AgronomistBillingLine {
  clientUserId: number;
  clientName?: string | null;
  clientEmail?: string | null;
  planName: string;
  monthlyPriceCents: number;
  includedReports: number;
  usedReports: number;
  overageReports: number;
  overagePriceCents: number;
  totalCents: number;
}

export interface AgronomistBilling {
  clients: AgronomistBillingLine[];
  totalCents: number;
  periodStart: string;
  periodEnd: string;
}
