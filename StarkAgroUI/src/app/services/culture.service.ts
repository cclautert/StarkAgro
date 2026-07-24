import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/**
 * Lista de culturas para os seletores (área, perfil de adubação, diagnóstico). Leitura para
 * qualquer usuário autenticado — o CRUD é do admin (AdminService).
 */
@Injectable({ providedIn: 'root' })
export class CultureService {
  private baseUrl = '/api/v1/cultures';

  constructor(private http: HttpClient) { }

  /** Nomes de culturas, já ordenados pelo servidor. */
  list(): Observable<string[]> {
    return this.http.get<string[]>(this.baseUrl);
  }
}
