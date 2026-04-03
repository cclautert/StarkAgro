import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {

  constructor(private router: Router) {}

  canActivate(): boolean | UrlTree {
    // In SSR/prerender there is no window/localStorage; treat as unauthenticated
    const isBrowser = typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
    const isAuthenticated = isBrowser ? !!localStorage.getItem('token') : false;

    if (!isAuthenticated) {
      return this.router.createUrlTree(['/login']);
    }

    return true;
  }
}
