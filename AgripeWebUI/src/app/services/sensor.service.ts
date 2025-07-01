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
  private baseUrl = 'https://localhost:7162/v1/'; //localhost URL para desenvolvimento
  //private readonly baseUrl = 'http://localhost:8080/v1/';

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

  // CREATE
  addSensor(sensor: Sensor): Observable<any> {
    return this.http.post(`${this.baseUrl}sensor/add`, sensor);
  }

  // UPDATE
  updateSensor(updatedSensor: Sensor): Observable<any> {
    return this.http.put(`${this.baseUrl}sensor/update`, updatedSensor);
  }

  // DELETE
  deleteSensor(id: number): Observable<{}> {
    const params = new HttpParams()
    .set('Id', id.toString());
    return this.http.delete(`${this.baseUrl}sensor/delete`, { params });
  }
}
