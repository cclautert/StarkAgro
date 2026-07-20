import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  constructor(private router: Router) {}

  canActivate(): boolean {
    const token = localStorage.getItem('token'); // ou outro critério de autenticação

    if (token) {
      return true;
    }

    // Se não estiver logado, redireciona para login
    this.router.navigate(['/login']);
    return false;
  }
}
