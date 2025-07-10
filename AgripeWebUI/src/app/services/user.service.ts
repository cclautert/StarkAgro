import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { User } from '../models/user.model';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private http = inject(HttpClient);

  //private baseUrl = 'https://localhost:7162/v1/'; //localhost URL para desenvolvimento
  private readonly baseUrl = 'http://localhost:8080/v1/';
  //private baseUrl = 'http://agripewebapi:8080/v1/'; // Azure | AWS

  // READ (by ID)
  getUserById(id: number): Observable<User> {
    const params = new HttpParams()
    .set('Id', id.toString());
    const sensor = this.http.get<User>(`${this.baseUrl}user/getById`, { params });
    return sensor;
  }

  // CREATE
  addUser(sensor: User): Observable<any> {
    return this.http.post(`${this.baseUrl}user/add`, sensor);
  }

  // UPDATE
  updateUser(updatedUser: User): Observable<any> {
    return this.http.put(`${this.baseUrl}user/update`, updatedUser);
  }

  // DELETE
  deleteUser(id: number): Observable<{}> {
    const params = new HttpParams()
    .set('Id', id.toString());
    return this.http.delete(`${this.baseUrl}user/delete`, { params });
  }
}
