import { Injectable, OnDestroy, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, interval, Observable, Subscription, of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { UserAlert } from '../models/alert.model';

const POLL_INTERVAL_MS = 60_000;

@Injectable({ providedIn: 'root' })
export class AlertService implements OnDestroy {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/';

  private alertsSubject = new BehaviorSubject<UserAlert[]>([]);
  private loadingSubject = new BehaviorSubject<boolean>(false);
  private errorSubject = new BehaviorSubject<boolean>(false);
  private pollSub: Subscription | null = null;

  alerts$ = this.alertsSubject.asObservable();
  loading$ = this.loadingSubject.asObservable();
  error$ = this.errorSubject.asObservable();

  get unreadCount(): number {
    return this.alertsSubject.value.filter(a => !a.isRead).length;
  }

  startPolling(): void {
    if (this.pollSub) return;
    this.fetch();
    this.pollSub = interval(POLL_INTERVAL_MS)
      .pipe(switchMap(() => this.fetchAlerts()))
      .subscribe();
  }

  stopPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
  }

  refresh(): void {
    this.fetch();
  }

  markAllRead(): void {
    const updated = this.alertsSubject.value.map(a => ({ ...a, isRead: true }));
    this.alertsSubject.next(updated);
    // Fire-and-forget mark-read endpoint when backend supports it
    this.http.post(`${this.baseUrl}user/alerts/mark-read`, {})
      .pipe(catchError(() => of(null)))
      .subscribe();
  }

  private fetch(): void {
    this.fetchAlerts().subscribe();
  }

  private fetchAlerts(): Observable<UserAlert[]> {
    this.loadingSubject.next(true);
    this.errorSubject.next(false);
    return this.http.get<UserAlert[]>(`${this.baseUrl}user/alerts`).pipe(
      tap(alerts => {
        this.alertsSubject.next(alerts);
        this.loadingSubject.next(false);
      }),
      catchError(() => {
        this.errorSubject.next(true);
        this.loadingSubject.next(false);
        return of(this.alertsSubject.value);
      })
    );
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }
}
