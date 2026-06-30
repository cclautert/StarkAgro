import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AdminUser } from '../models/admin-user.model';
import { PlatformAiSettings } from '../models/platform-ai-settings.model';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/admin/';

  getAllUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(`${this.baseUrl}users`);
  }

  createUser(data: Partial<AdminUser> & { password: string }): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.baseUrl}users`, data);
  }

  updateUser(id: number, data: Partial<AdminUser> & { password?: string }): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.baseUrl}users/${id}`, data);
  }

  toggleActive(id: number, active: boolean): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.baseUrl}users/${id}/toggle-active`, { active });
  }

  deleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}users/${id}`);
  }

  getAiSettings(): Observable<PlatformAiSettings> {
    return this.http.get<PlatformAiSettings>(`${this.baseUrl}ai-settings`);
  }

  updateAiSettings(data: PlatformAiSettings): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}ai-settings`, data);
  }
}
