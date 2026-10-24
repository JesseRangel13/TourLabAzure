import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideStore, Store } from '@ngxs/store';
import { AuthState, LoadCurrentUser, Login, Logout, ResetSession } from './auth.state';
import { ScaffoldState } from '../scaffold.state';
import { sessionExpiryInterceptor } from './session.interceptor';

describe('auth session state', () => {
  let store: Store;
  let http: HttpTestingController;
  const alice = { userId: 'alice-id', userName: 'Alice', role: 'Tourist' };
  const bob = { userId: 'bob-id', userName: 'Bob', role: 'Tourist' };
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([sessionExpiryInterceptor])), provideHttpClientTesting(),
      provideStore([AuthState, ScaffoldState])
    ] });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('treats an anonymous401 as a known signed-out session', () => {
    let complete = false;
    store.dispatch(new LoadCurrentUser()).subscribe({ complete: () => complete = true });
    http.expectOne('/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(complete).toBe(true);
    expect(store.selectSnapshot(AuthState.view)).toEqual({ user: null, loading: false, initialized: true, error: null });
  });

  it('refreshes CSRF after login and never stores the password', () => {
    const action = new Login('Alice', 'private-password');
    let complete = false;
    store.dispatch(action).subscribe({ complete: () => complete = true });
    http.expectOne('/auth/csrf').flush({ requestToken: 'anonymous' });
    const login = http.expectOne('/auth/login');
    expect(login.request.body).toEqual({ userName: 'Alice', password: 'private-password' });
    login.flush(alice);
    expect(complete).toBe(false);
    http.expectOne('/auth/csrf').flush({ requestToken: 'authenticated' });
    expect(complete).toBe(true);
    expect(store.selectSnapshot(AuthState.view).user).toEqual(alice);
    expect(JSON.stringify(store.snapshot())).not.toContain('private-password');
    expect(action.password).toBe('');
  });

  it('propagates login failure, clears loading and scrubs transient credentials', () => {
    const action = new Login('Alice', 'private-password');
    let failed = false;
    store.dispatch(action).subscribe({ error: () => failed = true });
    http.expectOne('/auth/csrf').flush({ requestToken: 'anonymous' });
    http.expectOne('/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(failed).toBe(true);
    expect(store.selectSnapshot(AuthState.view).loading).toBe(false);
    expect(store.selectSnapshot(AuthState.view).error).toBe('Invalid user name or password.');
    expect(action.password).toBe('');
  });

  it('ignores an old current-user response after a new login starts', () => {
    store.dispatch(new LoadCurrentUser()).subscribe();
    const obsolete = http.expectOne('/auth/me');
    store.dispatch(new Login('Alice', 'private-password')).subscribe();
    http.expectOne('/auth/csrf').flush({ requestToken: 'anonymous' });
    http.expectOne('/auth/login').flush(alice);
    http.expectOne('/auth/csrf').flush({ requestToken: 'authenticated' });
    obsolete.flush(bob);
    expect(store.selectSnapshot(AuthState.view).user).toEqual(alice);
  });

  it('clears per-user state immediately on logout and resets loading even if the server fails', () => {
    store.reset({ ...store.snapshot(), auth: { user: alice, initialized: true, loading: false, error: null },
      scaffold: { loading: false, status: 'Alive', error: null } });
    store.dispatch(new Logout()).subscribe({ error: () => {} });
    expect(store.selectSnapshot(AuthState.view).user).toBeNull();
    expect(store.selectSnapshot(ScaffoldState.view).status).toBe('Not checked');
    http.expectOne('/auth/csrf').flush({ requestToken: 'authenticated' });
    http.expectOne('/auth/logout').flush({}, { status: 503, statusText: 'Unavailable' });
    expect(store.selectSnapshot(AuthState.view).loading).toBe(false);
    expect(store.selectSnapshot(AuthState.view).error).toContain('Sign out could not be completed');
  });

  it('does not allow a previous response to restore a reset session', () => {
    store.dispatch(new LoadCurrentUser()).subscribe();
    const pending = http.expectOne('/auth/me');
    store.dispatch(new ResetSession('Session expired.')).subscribe();
    pending.flush(alice);
    expect(store.selectSnapshot(AuthState.view).user).toBeNull();
    expect(store.selectSnapshot(AuthState.view).error).toBe('Session expired.');
  });

  it('rejects concurrent cookie mutations rather than racing login and logout responses', () => {
    store.dispatch(new Login('Alice', 'private-password')).subscribe();
    let rejected = false;
    store.dispatch(new Logout()).subscribe({ error: () => rejected = true });
    expect(rejected).toBe(true);
    http.expectOne('/auth/csrf').flush({ requestToken: 'anonymous' });
    http.expectOne('/auth/login').flush(alice);
    http.expectOne('/auth/csrf').flush({ requestToken: 'authenticated' });
    expect(store.selectSnapshot(AuthState.view).user).toEqual(alice);
  });
});
