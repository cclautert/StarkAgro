import { Injectable, inject } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { HttpClient } from '@angular/common/http';
import { catchError, firstValueFrom, of } from 'rxjs';
import { environment } from '../../environments/environment';

export type EnableNotificationsResult = 'granted' | 'denied' | 'unsupported' | 'error';

@Injectable({ providedIn: 'root' })
export class WebPushService {
  private swPush = inject(SwPush);
  private http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/';

  /** Current browser permission, or 'unsupported' when the Notification API is absent (SSR, iOS Safari tab). */
  get permission(): NotificationPermission | 'unsupported' {
    if (typeof Notification === 'undefined') return 'unsupported';
    return Notification.permission;
  }

  get isSupported(): boolean {
    return (
      this.swPush.isEnabled &&
      typeof Notification !== 'undefined' &&
      !!environment.vapidPublicKey &&
      environment.vapidPublicKey !== 'CHANGE_ME'
    );
  }

  /** iOS only exposes web push to apps installed on the home screen (iOS 16.4+). */
  get needsHomeScreenInstall(): boolean {
    if (typeof window === 'undefined' || typeof navigator === 'undefined') return false;
    const isIos = /iPhone|iPad|iPod/.test(navigator.userAgent);
    const standalone =
      (navigator as any).standalone === true ||
      (typeof window.matchMedia === 'function' && window.matchMedia('(display-mode: standalone)').matches);
    return isIos && !standalone && typeof Notification === 'undefined';
  }

  /**
   * Must be called from a user gesture (click/tap) — iOS and Chrome
   * suppress permission prompts that are not gesture-initiated.
   */
  async enableNotifications(): Promise<EnableNotificationsResult> {
    if (!this.isSupported) return 'unsupported';
    if (this.permission === 'denied') return 'denied';

    try {
      const subscribed = await this.subscribeAndRegister();
      if (!subscribed) return this.deniedOrError();
      return 'granted';
    } catch {
      return this.deniedOrError();
    }
  }

  /**
   * Silent path called on app load: never prompts — only refreshes the
   * subscription when permission was already granted via enableNotifications().
   */
  async initialize(): Promise<void> {
    if (!this.isSupported || this.permission !== 'granted') return;

    try {
      await this.subscribeAndRegister();
    } catch {
      // Subscription refresh failed — non-critical
    }
  }

  private deniedOrError(): EnableNotificationsResult {
    return this.permission === 'denied' ? 'denied' : 'error';
  }

  private async subscribeAndRegister(): Promise<boolean> {
    const subscription = await this.swPush.requestSubscription({
      serverPublicKey: environment.vapidPublicKey
    });
    if (!subscription) return false;

    await firstValueFrom(
      this.http
        .put(`${this.baseUrl}user/webPushSubscription`, {
          subscriptionJson: JSON.stringify(subscription)
        })
        .pipe(catchError(() => of(null)))
    );
    return true;
  }
}
