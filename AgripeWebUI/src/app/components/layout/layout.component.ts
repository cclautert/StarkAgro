import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, map, Observable, shareReplay } from 'rxjs';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { IdleTimeoutService } from '../../services/idle-timeout.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [
    CommonModule,
    MatSidenavModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    RouterModule
  ],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.css'
})
export class LayoutComponent implements OnInit, OnDestroy {
  showLayout: boolean = false;
  @ViewChild('sidenav') sidenav!: MatSidenav;

  isSmallScreen$!: Observable<boolean>;

  constructor(
    private router: Router,
    private breakpointObserver: BreakpointObserver,
    private idleTimeoutService: IdleTimeoutService
  ) {
    this.isSmallScreen$ = this.breakpointObserver.observe(Breakpoints.Handset)
      .pipe(
        map(result => result.matches),
        shareReplay()
      );
  }

  ngOnInit(): void {
    // Set initial state from current URL so menu shows on first load (e.g. /home)
    const url = this.router.url.replace(/^\//, '') || '';
    this.showLayout = !url.startsWith('login');

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        const isLogin = event.urlAfterRedirects.startsWith('/login');
        this.showLayout = !isLogin;
        if (!isLogin) {
          this.idleTimeoutService.start();
        } else {
          this.idleTimeoutService.stop();
        }
      });

    if (this.showLayout) {
      this.idleTimeoutService.start();
    }
  }

  ngOnDestroy(): void {
    this.idleTimeoutService.stop();
  }

  logout(): void {
    if (typeof window !== 'undefined' && typeof window.localStorage !== 'undefined') {
      localStorage.removeItem('token');
    }
    this.router.navigate(['/login']);
  }
}
