import { Injectable, NgZone, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { fromEvent, merge, Subscription } from 'rxjs';

const TIMEOUT_MS = 10 * 60 * 1000; // 10 minutes

@Injectable({ providedIn: 'root' })
export class IdleTimeoutService implements OnDestroy {
  private timer: ReturnType<typeof setTimeout> | null = null;
  private eventSub: Subscription | null = null;
  private running = false;

  constructor(private router: Router, private ngZone: NgZone) {}

  start(): void {
    if (this.running) return;
    this.running = true;

    this.ngZone.runOutsideAngular(() => {
      this.eventSub = merge(
        fromEvent(document, 'mousemove'),
        fromEvent(document, 'click'),
        fromEvent(document, 'keydown'),
        fromEvent(document, 'touchstart')
      ).subscribe(() => this.reset());

      this.scheduleTimeout();
    });
  }

  stop(): void {
    this.running = false;
    this.clearTimer();
    this.eventSub?.unsubscribe();
    this.eventSub = null;
  }

  private reset(): void {
    this.clearTimer();
    this.scheduleTimeout();
  }

  private scheduleTimeout(): void {
    this.timer = setTimeout(() => {
      this.ngZone.run(() => {
        localStorage.removeItem('token');
        localStorage.removeItem('userId');
        this.router.navigate(['/login']);
      });
    }, TIMEOUT_MS);
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  ngOnDestroy(): void {
    this.stop();
  }
}
