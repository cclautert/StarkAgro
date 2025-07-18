// src/app/services/pivot.service.ts

import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { Pivot } from '../models/pivot.model';
import { HttpClient, HttpParams } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class PivotService {
  //private baseUrl = 'https://localhost:7162/v1/'; //DEBUG
  //private baseUrl = 'http://localhost:8080/v1/';
  private baseUrl = 'http://15.229.6.106:8080/v1/'; // Azure | AWS

  constructor(private http: HttpClient) { }

  // READ (All)
  getPivots(): Observable<Pivot[]> {
    return this.http.get<Pivot[]>(`${this.baseUrl}pivot/getAll`);
  }

  // READ (Single)
  getPivotById(id: number): Observable<Pivot | undefined> {
    const params = new HttpParams()
    .set('Id', id.toString());
    const pivot = this.http.get<Pivot>(`${this.baseUrl}pivot/getById`, { params });
    return pivot;
  }

  // CREATE
  addPivot(pivot: Pivot): Observable<any> {
    return this.http.post(`${this.baseUrl}pivot/add`, pivot);
  }

  // UPDATE
  updatePivot(updatedPivot: Pivot): Observable<any> {
    return this.http.put(`${this.baseUrl}pivot/update`, updatedPivot);
  }

  // DELETE
  deletePivot(id: number): Observable<{}> {
    const params = new HttpParams()
    .set('Id', id.toString());
    return this.http.delete(`${this.baseUrl}pivot/delete`, { params });
  }
}
