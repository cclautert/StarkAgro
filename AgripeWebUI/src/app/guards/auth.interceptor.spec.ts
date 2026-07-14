import { TestBed } from '@angular/core/testing';
import { HttpHandler, HttpRequest, HttpResponse } from '@angular/common/http';
import { of } from 'rxjs';

import { AuthInterceptor } from './auth.interceptor';

describe('AuthInterceptor', () => {
  let interceptor: AuthInterceptor;
  let next: HttpHandler;
  let handled: HttpRequest<unknown>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AuthInterceptor] });
    interceptor = TestBed.inject(AuthInterceptor);

    next = {
      handle: (req: HttpRequest<unknown>) => {
        handled = req;
        return of(new HttpResponse());
      }
    } as HttpHandler;

    localStorage.removeItem('token');
  });

  afterEach(() => localStorage.removeItem('token'));

  it('anexa o Bearer quando há token', () => {
    localStorage.setItem('token', 'abc123');

    interceptor.intercept(new HttpRequest('GET', '/api/v1/pivot'), next).subscribe();

    expect(handled.headers.get('Authorization')).toBe('Bearer abc123');
  });

  it('não manda Authorization quando não há token', () => {
    interceptor.intercept(new HttpRequest('GET', '/api/v1/pivot'), next).subscribe();

    expect(handled.headers.has('Authorization')).toBeFalse();
  });
});
