import { Injectable, inject } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { HttpClient } from '@angular/common/http';
import { catchError, of } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class WebPushService {
  private swPush = inject(SwPush);
  private http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/';

  async initialize(): Promise<void> {
    if (!this.swPush.isEnabled) return;
    if (typeof Notification === 'undefined' || Notification.permission === 'denied') return;
    if (!environment.vapidPublicKey || environment.vapidPublicKey === 'CHANGE_ME') return;

    try {
      const subscription = await this.swPush.requestSubscription({
        serverPublicKey: environment.vapidPublicKey
      });

      this.http
        .put(`${this.baseUrl}user/webPushSubscription`, {
          subscriptionJson: JSON.stringify(subscription)
        })
        .pipe(catchError(() => of(null)))
        .subscribe();

      this.swPush.messages.subscribe(message => {
        console.log('[WebPush] foreground message:', message);
      });
    } catch {
      // Permission denied or subscription failed — non-critical
    }
  }
}
