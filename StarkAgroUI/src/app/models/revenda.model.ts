export interface Revenda {
  id: number;
  name: string;
  cnpj?: string | null;
  contactEmail?: string | null;
  diagnosisPlanId?: number | null;
  diagnosisQuotaPerMonth?: number | null;
  active: boolean;
  createdAt: string;
}

export interface RevendaMember {
  id: number;
  memberUserId?: number | null;
  memberEmail: string;
  memberName?: string | null;
  memberRole: string; // Manager | Agronomist | Client
  status: string;     // Pending | Active | Declined | Revoked | Expired
  invitedAt: string;
  inviteExpiresAt: string;
  acceptedAt?: string | null;
}

export interface RevendaInvite {
  id: number;
  revendaId: number;
  revendaName?: string | null;
  memberRole: string;
  invitedAt: string;
  inviteExpiresAt: string;
}

export interface RevendaBillingClientLine {
  clientUserId: number;
  clientName?: string | null;
  clientEmail?: string | null;
  usedReports: number;
}

export interface RevendaBilling {
  revendaId: number;
  revendaName: string;
  planId?: number | null;
  planName: string;
  monthlyPriceCents: number;
  includedReports: number;
  usedReports: number;
  overageReports: number;
  overagePriceCents: number;
  totalCents: number;
  clients: RevendaBillingClientLine[];
  periodStart: string;
  periodEnd: string;
}
