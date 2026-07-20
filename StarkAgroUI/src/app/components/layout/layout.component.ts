import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatBadgeModule } from '@angular/material/badge';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, map, Observable, shareReplay } from 'rxjs';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { IdleTimeoutService } from '../../services/idle-timeout.service';
import { AlertService } from '../../services/alert.service';
import { WebPushService } from '../../services/web-push.service';
import { UserAlert } from '../../models/alert.model';

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
    MatBadgeModule,
    RouterModule
  ],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.css'
})
export class LayoutComponent implements OnInit, OnDestroy {
  showLayout: boolean = false;
  alertPanelOpen = false;
  isAdmin = typeof window !== 'undefined' && localStorage.getItem('isAdmin') === 'true';
  isAgronomist = typeof window !== 'undefined' && localStorage.getItem('isAgronomist') === 'true';
  isResellerManager = typeof window !== 'undefined' && localStorage.getItem('isResellerManager') === 'true';

  @ViewChild('sidenav') sidenav!: MatSidenav;

  isSmallScreen$!: Observable<boolean>;
  alerts$: Observable<UserAlert[]>;
  loading$: Observable<boolean>;
  error$: Observable<boolean>;

  constructor(
    private router: Router,
    private breakpointObserver: BreakpointObserver,
    private idleTimeoutService: IdleTimeoutService,
    public alertService: AlertService,
    private webPushService: WebPushService
  ) {
    this.isSmallScreen$ = this.breakpointObserver.observe(Breakpoints.Handset)
      .pipe(
        map(result => result.matches),
        shareReplay()
      );
    this.alerts$ = this.alertService.alerts$;
    this.loading$ = this.alertService.loading$;
    this.error$ = this.alertService.error$;
  }

  ngOnInit(): void {
    const url = this.router.url.replace(/^\//, '') || '';
    this.showLayout = !url.startsWith('login');

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        const isLogin = event.urlAfterRedirects.startsWith('/login');
        this.showLayout = !isLogin;
        if (!isLogin) {
          this.idleTimeoutService.start();
          this.alertService.startPolling();
        } else {
          this.idleTimeoutService.stop();
          this.alertService.stopPolling();
        }
      });

    if (this.showLayout) {
      this.idleTimeoutService.start();
      this.alertService.startPolling();
      this.webPushService.initialize().catch(() => {});
    }
  }

  ngOnDestroy(): void {
    this.idleTimeoutService.stop();
    this.alertService.stopPolling();
  }

  toggleAlertPanel(): void {
    this.alertPanelOpen = !this.alertPanelOpen;
    if (this.alertPanelOpen) {
      this.alertService.markAllRead();
    }
  }

  closeAlertPanel(): void {
    this.alertPanelOpen = false;
  }

  refreshAlerts(event: MouseEvent): void {
    event.stopPropagation();
    this.alertService.refresh();
  }

  alertTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      MoistureLow: 'Umidade baixa',
      AnomalyPersisted: 'Anomalia persistente',
      AgronomistInvite: 'Convite de agrônomo'
    };
    return labels[type] ?? type;
  }

  isInvite(alert: UserAlert): boolean {
    return alert.alertType === 'AgronomistInvite';
  }

  /** O convite é a única notificação que exige uma resposta — leva o produtor até onde ele responde. */
  openAlert(alert: UserAlert): void {
    if (!this.isInvite(alert)) return;
    this.closeAlertPanel();
    this.router.navigate(['/diagnosticos']);
  }

  logout(): void {
    if (typeof window !== 'undefined' && typeof window.localStorage !== 'undefined') {
      localStorage.removeItem('token');
      localStorage.removeItem('isAdmin');
      localStorage.removeItem('isAgronomist');
      localStorage.removeItem('isResellerManager');
    }
    this.router.navigate(['/login']);
  }
}
