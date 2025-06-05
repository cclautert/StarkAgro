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
  private baseUrl = 'http://192.168.68.33:8080/v1/';

  constructor(private http: HttpClient) { }

  login(email: string, password: string): Observable<{ token: string }> {
    const body = { email, password };

    return this.http.post<{ token: string }>(`${this.baseUrl}Auth/LogIn`, body);
  }

  getReads(userId: number, numberOfReads: number): Observable<Read[]> {
    const params = new HttpParams()
    .set('UserId', userId.toString())
    .set('NumberOfReads', numberOfReads.toString());

    return this.http.get<Read[]>(`${this.baseUrl}reads/getall`, { params });
  }

  addPivot(pivot: Pivot): Observable<any> {
    return this.http.post(`${this.baseUrl}pivot/add`, pivot);
  }

  addUser(user: User): Observable<any> {
    return this.http.post(`${this.baseUrl}user/add`, user);
  }

  addSensor(sensor: Sensor): Observable<any> {
    return this.http.post(`${this.baseUrl}sensor/add`, sensor);
  }
}
