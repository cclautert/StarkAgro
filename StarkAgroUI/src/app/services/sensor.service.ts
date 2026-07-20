import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Sensor } from '../models/sensor.model';

@Injectable({
  providedIn: 'root'
})
export class SensorService {
  // Injeção de dependência moderna com inject()
  private http = inject(HttpClient);

  // URL base da sua API
  /** In dev, use relative URL so ng serve proxy forwards /api to the API (avoids CORS). */
  private baseUrl = '/api/v1/';

  // READ (All)
  getSensores(): Observable<Sensor[]> {
    return this.http.get<Sensor[]>(`${this.baseUrl}sensor/getAll`);
  }

  // READ (by ID)
  getSensorById(id: number): Observable<Sensor> {
    const params = new HttpParams()
    .set('Id', id.toString());
    const sensor = this.http.get<Sensor>(`${this.baseUrl}sensor/getById`, { params });
    return sensor;
  }

  getAllByPivotId(id: number, quadrante: number): Observable<Sensor[]> {
    const params = new HttpParams()
    .set('PivotId', id.toString())
    .set('Quadrante', quadrante.toString());
    return this.http.get<Sensor[]>(`${this.baseUrl}sensor/getAllByPivotId`, { params });
  }

  // CREATE
  addSensor(sensor: Sensor): Observable<any> {
    return this.http.post(`${this.baseUrl}sensor/add`, sensor);
  }

  // UPDATE
  updateSensor(updatedSensor: Sensor): Observable<any> {
    return this.http.put(`${this.baseUrl}sensor/update`, updatedSensor);
  }

  syncDownlink(sensorId: number): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.baseUrl}sensor/sync-downlink`, { sensorId }
    );
  }

  // DELETE
  deleteSensor(id: number): Observable<{}> {
    const params = new HttpParams()
    .set('Id', id.toString());
    return this.http.delete(`${this.baseUrl}sensor/delete`, { params });
  }
}
