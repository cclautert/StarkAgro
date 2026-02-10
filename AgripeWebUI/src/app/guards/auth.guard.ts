import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {

  constructor(private router: Router) {}

  canActivate(): boolean {
    // In SSR/prerender there is no window/localStorage; treat as unauthenticated
    const isBrowser = typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
    const isAuthenticated = isBrowser ? !!localStorage.getItem('token') : false;

    if (!isAuthenticated) {
      this.router.navigate(['/login']);
      return false;
    }

    return true;
  }
}
