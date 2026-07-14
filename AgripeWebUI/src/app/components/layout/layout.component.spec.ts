import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of } from 'rxjs';

import { LayoutComponent } from './layout.component';
import { AlertService } from '../../services/alert.service';
import { IdleTimeoutService } from '../../services/idle-timeout.service';
import { WebPushService } from '../../services/web-push.service';
import { UserAlert } from '../../models/alert.model';

describe('LayoutComponent — convite de agrônomo no sininho', () => {
  let component: LayoutComponent;
  let router: Router;

  const invite: UserAlert = {
    id: 'invite-1',
    title: 'Agrônomo Teste quer acompanhar seus laudos. Toque para responder ao convite.',
    pivotName: '—',
    alertType: 'AgronomistInvite',
    createdAt: new Date().toISOString(),
    isRead: false
  };

  const moisture: UserAlert = { ...invite, id: 'irrigation-1', alertType: 'MoistureLow', pivotName: 'Pivô 1' };

  beforeEach(async () => {
    const alertService = {
      alerts$: of([invite]),
      loading$: of(false),
      error$: of(false),
      unreadCount: 1,
      startPolling: () => {},
      stopPolling: () => {},
      markAllRead: () => {},
      refresh: () => {}
    };

    await TestBed.configureTestingModule({
      imports: [LayoutComponent, RouterTestingModule, NoopAnimationsModule],
      providers: [
        { provide: AlertService, useValue: alertService },
        { provide: IdleTimeoutService, useValue: { start: () => {}, stop: () => {} } },
        { provide: WebPushService, useValue: { initialize: () => Promise.resolve() } }
      ]
    }).compileComponents();

    component = TestBed.createComponent(LayoutComponent).componentInstance;
    router = TestBed.inject(Router);
  });

  it('rotula o convite em português', () => {
    expect(component.alertTypeLabel('AgronomistInvite')).toBe('Convite de agrônomo');
  });

  it('reconhece o convite e não confunde com os demais alertas', () => {
    expect(component.isInvite(invite)).toBeTrue();
    expect(component.isInvite(moisture)).toBeFalse();
  });

  it('leva o produtor até os laudos ao tocar no convite — era o passo que faltava', () => {
    const navigate = spyOn(router, 'navigate');
    component.alertPanelOpen = true;

    component.openAlert(invite);

    expect(navigate).toHaveBeenCalledWith(['/diagnosticos']);
    expect(component.alertPanelOpen).toBeFalse();
  });

  it('não navega ao tocar num alerta que não é convite', () => {
    const navigate = spyOn(router, 'navigate');

    component.openAlert(moisture);

    expect(navigate).not.toHaveBeenCalled();
  });
});
