import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PlantDiagnosis } from '../models/plant-diagnosis.model';
import {
  AgronomistClient,
  AgronomistInvite,
  AgronomistQueueItem,
  SignPayload
} from '../models/agronomist.model';
import { AgronomistBilling } from '../models/agronomist-billing.model';

@Injectable({
  providedIn: 'root'
})
export class AgronomistService {
  private baseUrl = '/api/v1/';

  constructor(private http: HttpClient) { }

  // ── Faturamento ─────────────────────────────────────────────────────────
  getBilling(): Observable<AgronomistBilling> {
    return this.http.get<AgronomistBilling>(`${this.baseUrl}agronomist/billing`);
  }

  // ── Fila ────────────────────────────────────────────────────────────────
  getQueue(status?: string, pageSize = 20, pageIndex = 0): Observable<AgronomistQueueItem[]> {
    let params = new HttpParams()
      .set('pageSize', pageSize.toString())
      .set('pageIndex', pageIndex.toString());

    if (status) params = params.set('status', status);

    return this.http.get<AgronomistQueueItem[]>(`${this.baseUrl}agronomist/queue`, { params });
  }

  getDiagnosis(id: number): Observable<PlantDiagnosis> {
    return this.http.get<PlantDiagnosis>(`${this.baseUrl}agronomist/diagnosis/${id}`);
  }

  /** A foto vem como blob: <img src> não envia o header Authorization. */
  getDiagnosisImage(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}agronomist/diagnosis/${id}/image`, { responseType: 'blob' });
  }

  /** PDF do laudo — mesma regra de acesso do detalhe. */
  getDiagnosisPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}agronomist/diagnosis/${id}/pdf`, { responseType: 'blob' });
  }

  claim(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}agronomist/diagnosis/${id}/claim`, {});
  }

  saveDraft(id: number, payload: SignPayload): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}agronomist/diagnosis/${id}/review`, payload);
  }

  sign(id: number, payload: SignPayload): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}agronomist/diagnosis/${id}/sign`, payload);
  }

  reject(id: number, reason: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}agronomist/diagnosis/${id}/reject`, { reason });
  }

  // ── Carteira de clientes ────────────────────────────────────────────────
  getClients(): Observable<AgronomistClient[]> {
    return this.http.get<AgronomistClient[]>(`${this.baseUrl}agronomist/clients`);
  }

  inviteClient(clientEmail: string): Observable<AgronomistClient> {
    return this.http.post<AgronomistClient>(`${this.baseUrl}agronomist/clients/invite`, { clientEmail });
  }

  revokeClient(linkId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}agronomist/clients/${linkId}`);
  }

  // ── Lado do produtor ────────────────────────────────────────────────────
  getMyInvites(): Observable<AgronomistInvite[]> {
    return this.http.get<AgronomistInvite[]>(`${this.baseUrl}user/agronomist-invites`);
  }

  acceptInvite(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}user/agronomist-invites/${id}/accept`, {});
  }

  declineInvite(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}user/agronomist-invites/${id}/decline`, {});
  }

  /** O produtor demite o agrônomo. */
  revokeMyAgronomist(): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}user/agronomist-link`);
  }
}
