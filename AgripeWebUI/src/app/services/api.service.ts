import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { Read } from '../models/read.model';
import { Pivot } from '../models/pivot.model';
import { User } from '../models/user.model';
import { Sensor } from '../models/sensor.model';
import { IrrigationTrend } from '../models/irrigation-trend.model';
import { SensorTelemetry } from '../models/sensor-telemetry.model';
import { HttpParams } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  /** In dev, use relative URL so ng serve proxy forwards /api to the API (avoids CORS). */
  private baseUrl = '/api/v1/';
  // For production or when not using proxy: 'https://localhost:7162/v1/' or your API origin

  constructor(private http: HttpClient) { }

  login(email: string, password: string): Observable<{ token: string }> {
    const body = { email, password };

    return this.http.post<{ token: string }>(`${this.baseUrl}Auth/LogIn`, body);
  }

  /**
   * OAuth 2.0: exchange authorization code from external provider (e.g. Google) for our JWT.
   */
  externalLogin(provider: string, code: string, redirectUri: string): Observable<{ token: string }> {
    const body = { provider, code, redirectUri };
    return this.http.post<{ token: string }>(`${this.baseUrl}Auth/external-login`, body);
  }

  getReads(userId: number, numberOfReads: number): Observable<Read[]> {
    const params = new HttpParams()
    .set('NumberOfReads', numberOfReads.toString());

    return this.http.get<Read[]>(`${this.baseUrl}reads/getall`, { params });
  }

  getAllReadsBySensorId(sensorId: number, quadrante: number, numberOfReads: number): Observable<Read[]> {
    const params = new HttpParams()
    .set('NumberOfReads', numberOfReads.toString())
    .set('Quadrante', quadrante.toString())
    .set('SensorId', sensorId.toString());
    return this.http.get<Read[]>(`${this.baseUrl}reads/GetAllBySensorId`, { params }).pipe(
      map(reads => reads.map(r => ({ ...r, value: r.humidity ?? r.value })))
    );
  }
  getReadsByPivotId(pivotId: number, numberOfReads: number): Observable<Pivot> {
    const params = new HttpParams()
    .set('PivotId', pivotId.toString())
    .set('NumberOfReads', numberOfReads.toString());

    return this.http.get<Pivot>(`${this.baseUrl}reads/GetByPivotId`, { params });
  }

  getIrrigationTrend(pivotId: number, numberOfReads: number = 10): Observable<IrrigationTrend> {
    const params = new HttpParams()
      .set('PivotId', pivotId.toString())
      .set('NumberOfReads', numberOfReads.toString());

    return this.http.get<IrrigationTrend>(`${this.baseUrl}pivot/getIrrigationTrend`, { params });
  }

  getSensorTelemetry(pivotId: number): Observable<SensorTelemetry[]> {
    const params = new HttpParams().set('PivotId', pivotId.toString());
    return this.http.get<SensorTelemetry[]>(`${this.baseUrl}sensor/telemetry`, { params });
  }

  addUser(user: User): Observable<any> {
    return this.http.post(`${this.baseUrl}user/add`, user);
  }
}
