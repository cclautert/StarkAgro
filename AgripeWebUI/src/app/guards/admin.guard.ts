import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class AdminGuard implements CanActivate {
  constructor(private router: Router) {}

  canActivate(): boolean | UrlTree {
    const isBrowser = typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
    const isAdmin = isBrowser ? localStorage.getItem('isAdmin') === 'true' : false;
    return isAdmin ? true : this.router.createUrlTree(['/home']);
  }
}
