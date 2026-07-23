import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AreaRequest, FetchNdviHistoryResponse, MonitoredArea, NdviTrendResponse } from '../models/monitored-area.model';

@Injectable({
  providedIn: 'root'
})
export class AreaService {
  /** Relativo para o proxy do ng serve encaminhar /api à API (evita CORS). */
  private baseUrl = '/api/v1/areas';

  constructor(private http: HttpClient) { }

  list(): Observable<MonitoredArea[]> {
    return this.http.get<MonitoredArea[]>(this.baseUrl);
  }

  get(id: number): Observable<MonitoredArea> {
    return this.http.get<MonitoredArea>(`${this.baseUrl}/${id}`);
  }

  create(body: AreaRequest): Observable<MonitoredArea> {
    return this.http.post<MonitoredArea>(this.baseUrl, body);
  }

  update(id: number, body: AreaRequest): Observable<MonitoredArea> {
    return this.http.put<MonitoredArea>(`${this.baseUrl}/${id}`, body);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  trend(id: number): Observable<NdviTrendResponse> {
    return this.http.get<NdviTrendResponse>(`${this.baseUrl}/${id}/trend`);
  }

  /** PNG protegido: buscar como blob (o interceptor injeta o Bearer; <img> não). */
  overlay(areaId: number, readingId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${areaId}/overlay/${readingId}`, { responseType: 'blob' });
  }

  /** GeoTIFF de zonas: gerado sob demanda no servidor, baixado como blob (Bearer via interceptor). */
  zones(areaId: number, readingId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${areaId}/zones/${readingId}`, { responseType: 'blob' });
  }

  /**
   * Histórico retroativo: "voltar no tempo" numa data. Grátis se a passagem já está armazenada;
   * consome cota de PU se o servidor precisar buscar na CDSE. `date` no formato yyyy-MM-dd.
   */
  history(areaId: number, date: string): Observable<FetchNdviHistoryResponse> {
    return this.http.get<FetchNdviHistoryResponse>(
      `${this.baseUrl}/${areaId}/history`, { params: { date } });
  }
}
