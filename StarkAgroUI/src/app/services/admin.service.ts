import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AdminUser } from '../models/admin-user.model';
import { PlatformAiSettings } from '../models/platform-ai-settings.model';
import { DiagnosisPlan } from '../models/diagnosis-plan.model';
import { FertilizationProfile } from '../models/fertilization-profile.model';
import { Culture } from '../models/culture.model';
import { Revenda, RevendaBilling } from '../models/revenda.model';

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

  getDiagnosisPlans(): Observable<DiagnosisPlan[]> {
    return this.http.get<DiagnosisPlan[]>(`${this.baseUrl}diagnosis-plans`);
  }

  createDiagnosisPlan(data: Partial<DiagnosisPlan>): Observable<DiagnosisPlan> {
    return this.http.post<DiagnosisPlan>(`${this.baseUrl}diagnosis-plans`, data);
  }

  updateDiagnosisPlan(id: number, data: Partial<DiagnosisPlan>): Observable<DiagnosisPlan> {
    return this.http.put<DiagnosisPlan>(`${this.baseUrl}diagnosis-plans/${id}`, data);
  }

  deleteDiagnosisPlan(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}diagnosis-plans/${id}`);
  }

  getFertilizationProfiles(): Observable<FertilizationProfile[]> {
    return this.http.get<FertilizationProfile[]>(`${this.baseUrl}fertilization-profiles`);
  }

  createFertilizationProfile(data: Partial<FertilizationProfile>): Observable<FertilizationProfile> {
    return this.http.post<FertilizationProfile>(`${this.baseUrl}fertilization-profiles`, data);
  }

  updateFertilizationProfile(id: number, data: Partial<FertilizationProfile>): Observable<FertilizationProfile> {
    return this.http.put<FertilizationProfile>(`${this.baseUrl}fertilization-profiles/${id}`, data);
  }

  deleteFertilizationProfile(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}fertilization-profiles/${id}`);
  }

  // ── Culturas ────────────────────────────────────────────────────────────
  getCultures(): Observable<Culture[]> {
    return this.http.get<Culture[]>(`${this.baseUrl}cultures`);
  }

  createCulture(name: string): Observable<Culture> {
    return this.http.post<Culture>(`${this.baseUrl}cultures`, { name });
  }

  renameCulture(id: number, name: string): Observable<Culture> {
    return this.http.put<Culture>(`${this.baseUrl}cultures/${id}`, { name });
  }

  deleteCulture(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}cultures/${id}`);
  }

  // ── Revendas ────────────────────────────────────────────────────────────
  getRevendas(): Observable<Revenda[]> {
    return this.http.get<Revenda[]>(`${this.baseUrl}revendas`);
  }

  createRevenda(data: Partial<Revenda>): Observable<Revenda> {
    return this.http.post<Revenda>(`${this.baseUrl}revendas`, data);
  }

  updateRevenda(id: number, data: Partial<Revenda>): Observable<Revenda> {
    return this.http.put<Revenda>(`${this.baseUrl}revendas/${id}`, data);
  }

  /** Gestor é identificado pelo e-mail — o admin não tem o id interno em mãos. */
  assignRevendaManager(id: number, email: string): Observable<Revenda> {
    return this.http.post<Revenda>(`${this.baseUrl}revendas/${id}/manager`, { email });
  }

  getRevendaBilling(id: number): Observable<RevendaBilling> {
    return this.http.get<RevendaBilling>(`${this.baseUrl}revendas/${id}/billing`);
  }
}
