import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class AgronomistGuard implements CanActivate {
  constructor(private router: Router) {}

  canActivate(): boolean | UrlTree {
    const isBrowser = typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
    const isAgronomist = isBrowser ? localStorage.getItem('isAgronomist') === 'true' : false;
    return isAgronomist ? true : this.router.createUrlTree(['/home']);
  }
}
