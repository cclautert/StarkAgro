import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class ResellerGuard implements CanActivate {
  constructor(private router: Router) {}

  canActivate(): boolean | UrlTree {
    const isBrowser = typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
    const isReseller = isBrowser ? localStorage.getItem('isResellerManager') === 'true' : false;
    return isReseller ? true : this.router.createUrlTree(['/home']);
  }
}
