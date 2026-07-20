import { Injectable } from '@angular/core';
import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreatePlantDiagnosisResult,
  DiagnosisAuditEntry,
  DiagnosisHistory,
  DiagnosisQuota,
  PlantDiagnosis,
  PlantDiagnosisStatusResult,
  PlantDiagnosisSummary
} from '../models/plant-diagnosis.model';

@Injectable({
  providedIn: 'root'
})
export class DiagnosisService {
  /** In dev, use relative URL so ng serve proxy forwards /api to the API (avoids CORS). */
  private baseUrl = '/api/v1/';

  constructor(private http: HttpClient) { }

  /** Envia a foto. Devolve eventos de progresso para a barra de upload. */
  create(formData: FormData): Observable<HttpEvent<CreatePlantDiagnosisResult>> {
    return this.http.post<CreatePlantDiagnosisResult>(`${this.baseUrl}diagnosis`, formData, {
      reportProgress: true,
      observe: 'events'
    });
  }

  getAll(status?: string, pageSize = 20, pageIndex = 0): Observable<PlantDiagnosisSummary[]> {
    let params = new HttpParams()
      .set('pageSize', pageSize.toString())
      .set('pageIndex', pageIndex.toString());

    if (status) {
      params = params.set('status', status);
    }

    return this.http.get<PlantDiagnosisSummary[]>(`${this.baseUrl}diagnosis`, { params });
  }

  getById(id: number): Observable<PlantDiagnosis> {
    return this.http.get<PlantDiagnosis>(`${this.baseUrl}diagnosis/${id}`);
  }

  getStatus(id: number): Observable<PlantDiagnosisStatusResult> {
    return this.http.get<PlantDiagnosisStatusResult>(`${this.baseUrl}diagnosis/${id}/status`);
  }

  /**
   * A imagem vem como blob porque <img src> não envia o header Authorization —
   * só o HttpClient passa pelo auth.interceptor. Quem consome deve dar
   * URL.revokeObjectURL na URL gerada ao destruir o componente.
   */
  getImage(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}diagnosis/${id}/image`, { responseType: 'blob' });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}diagnosis/${id}`);
  }

  /** PDF do laudo — vem como blob pelo mesmo motivo da imagem (header Authorization). */
  getPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}diagnosis/${id}/pdf`, { responseType: 'blob' });
  }

  /** Reenfileira um laudo que falhou. A foto continua no servidor. */
  reprocess(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}diagnosis/${id}/reprocess`, {});
  }

  getHistory(pivotId: number): Observable<DiagnosisHistory> {
    return this.http.get<DiagnosisHistory>(`${this.baseUrl}diagnosis/history/${pivotId}`);
  }

  getAudit(id: number): Observable<DiagnosisAuditEntry[]> {
    return this.http.get<DiagnosisAuditEntry[]>(`${this.baseUrl}diagnosis/${id}/audit`);
  }

  /** Quantos laudos ainda restam no mês. */
  getQuota(): Observable<DiagnosisQuota> {
    return this.http.get<DiagnosisQuota>(`${this.baseUrl}diagnosis/quota`);
  }
}
