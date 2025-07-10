import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Read } from '../models/read.model';
import { Pivot } from '../models/pivot.model';
import { User } from '../models/user.model';
import { Sensor } from '../models/sensor.model';
import { HttpParams } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  //private baseUrl = 'https://localhost:7162/v1/';//local
  private baseUrl = 'http://localhost:8080/v1/';
  //private baseUrl = 'http://agripewebapi:8080/v1/'; // Azure | AWS

  constructor(private http: HttpClient) { }

  login(email: string, password: string): Observable<{ token: string }> {
    const body = { email, password };

    return this.http.post<{ token: string }>(`${this.baseUrl}Auth/LogIn`, body);
  }

  getReads(userId: number, numberOfReads: number): Observable<Read[]> {
    const params = new HttpParams()
    .set('NumberOfReads', numberOfReads.toString());

    return this.http.get<Read[]>(`${this.baseUrl}reads/getall`, { params });
  }

  getAllReadsByPivotId(sensorId: number, quadrante: number, numberOfReads: number): Observable<Read[]> {
    const params = new HttpParams()
    .set('NumberOfReads', numberOfReads.toString())
    .set('Quadrante', quadrante.toString())
    .set('SensorId', sensorId.toString());
    return this.http.get<Read[]>(`${this.baseUrl}reads/GetAllByPivotId`, { params });
  }
  getReadsByPivotId(pivotId: number, numberOfReads: number): Observable<Pivot> {
    const params = new HttpParams()
    .set('PivotId', pivotId.toString())
    .set('NumberOfReads', numberOfReads.toString());

    return this.http.get<Pivot>(`${this.baseUrl}reads/GetByPivotId`, { params });
  }

  addUser(user: User): Observable<any> {
    return this.http.post(`${this.baseUrl}user/add`, user);
  }
}
