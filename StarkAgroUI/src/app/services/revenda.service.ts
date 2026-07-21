import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Revenda, RevendaMember, RevendaInvite, RevendaBilling, RevendaSeats } from '../models/revenda.model';

@Injectable({ providedIn: 'root' })
export class RevendaService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/';

  // ── Gestor ────────────────────────────────────────────────────────────────
  getMyRevenda(): Observable<Revenda> {
    return this.http.get<Revenda>(`${this.baseUrl}revenda/me`);
  }

  getMembers(): Observable<RevendaMember[]> {
    return this.http.get<RevendaMember[]>(`${this.baseUrl}revenda/members`);
  }

  /** Ocupação de assentos — endpoint enxuto, separado do faturamento. */
  getSeats(): Observable<RevendaSeats> {
    return this.http.get<RevendaSeats>(`${this.baseUrl}revenda/seats`);
  }

  invite(email: string, role: string): Observable<RevendaMember> {
    return this.http.post<RevendaMember>(`${this.baseUrl}revenda/members/invite`, { email, role });
  }

  revokeMember(linkId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}revenda/members/${linkId}`);
  }

  getBilling(): Observable<RevendaBilling> {
    return this.http.get<RevendaBilling>(`${this.baseUrl}revenda/billing`);
  }

  // ── Lado do membro convidado ──────────────────────────────────────────────
  getMyInvites(): Observable<RevendaInvite[]> {
    return this.http.get<RevendaInvite[]>(`${this.baseUrl}user/revenda-invites`);
  }

  acceptInvite(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}user/revenda-invites/${id}/accept`, {});
  }

  declineInvite(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}user/revenda-invites/${id}/decline`, {});
  }
}
