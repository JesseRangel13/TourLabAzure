import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideStore, Store } from '@ngxs/store';
import { firstValueFrom, Observable } from 'rxjs';
import { AuthState } from './auth.state';
import { authGuard } from './auth.guard';

describe('protected account navigation', () => {
  beforeEach(() => TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideStore([AuthState]), provideRouter([])]
  }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('rechecks an initialized session and redirects an expired cookie to login', async () => {
    const store = TestBed.inject(Store);
    store.reset({ auth: { user: { userId: 'alice', userName: 'Alice', role: 'Tourist' }, initialized: true, loading: false, error: null } });
    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url: '/account' } as RouterStateSnapshot));
    const completed = firstValueFrom(result as Observable<boolean | UrlTree>);
    TestBed.inject(HttpTestingController).expectOne('/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect((await completed).toString()).toContain('/login');
    expect(store.selectSnapshot(AuthState.view).user).toBeNull();
  });

  it('permits navigation only after an authoritative current-user response', async () => {
    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url: '/account' } as RouterStateSnapshot));
    const completed = firstValueFrom(result as Observable<boolean | UrlTree>);
    TestBed.inject(HttpTestingController).expectOne('/auth/me').flush({ userId: 'alice', userName: 'Alice', role: 'Tourist' });
    expect(await completed).toBe(true);
  });
});
