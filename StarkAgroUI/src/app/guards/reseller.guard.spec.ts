import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { ResellerGuard } from './reseller.guard';

describe('ResellerGuard', () => {
  let guard: ResellerGuard;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    const routerSpy = jasmine.createSpyObj('Router', ['createUrlTree']);
    routerSpy.createUrlTree.and.returnValue({} as UrlTree);
    TestBed.configureTestingModule({
      providers: [ResellerGuard, { provide: Router, useValue: routerSpy }]
    });
    guard = TestBed.inject(ResellerGuard);
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
  });

  afterEach(() => localStorage.removeItem('isResellerManager'));

  it('permite quando isResellerManager é true', () => {
    localStorage.setItem('isResellerManager', 'true');
    expect(guard.canActivate()).toBe(true);
  });

  it('redireciona para /home quando não é gestor de revenda', () => {
    localStorage.removeItem('isResellerManager');
    guard.canActivate();
    expect(router.createUrlTree).toHaveBeenCalledWith(['/home']);
  });
});
