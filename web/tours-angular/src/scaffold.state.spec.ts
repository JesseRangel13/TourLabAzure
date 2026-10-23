import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideStore, Store } from '@ngxs/store';
import { LoadGatewayStatus, ScaffoldState } from './scaffold.state';

describe('gateway status action', () => {
  let store: Store;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideStore([ScaffoldState])] });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('completes only after the HTTP response and clears loading', () => {
    let completed = false;
    store.dispatch(new LoadGatewayStatus()).subscribe({ complete: () => completed = true });
    expect(store.selectSnapshot(ScaffoldState.view).loading).toBe(true);
    expect(completed).toBe(false);
    http.expectOne('/health/live').flush({ status: 'Alive' });
    expect(completed).toBe(true);
    expect(store.selectSnapshot(ScaffoldState.view)).toEqual({ loading: false, status: 'Alive', error: null });
  });

  it('propagates failure and permits a successful retry', () => {
    let failed = false;
    store.dispatch(new LoadGatewayStatus()).subscribe({ error: () => failed = true });
    http.expectOne('/health/live').flush({}, { status: 503, statusText: 'Unavailable' });
    expect(failed).toBe(true);
    expect(store.selectSnapshot(ScaffoldState.view).loading).toBe(false);
    expect(store.selectSnapshot(ScaffoldState.view).error).toBe('Gateway check failed.');
    store.dispatch(new LoadGatewayStatus()).subscribe();
    http.expectOne('/health/live').flush({ status: 'Alive' });
    expect(store.selectSnapshot(ScaffoldState.view).error).toBeNull();
  });

  it('cancels an obsolete request before a new result is applied', () => {
    store.dispatch(new LoadGatewayStatus()).subscribe();
    const obsolete = http.expectOne('/health/live');
    store.dispatch(new LoadGatewayStatus()).subscribe();
    expect(obsolete.cancelled).toBe(true);
    http.expectOne('/health/live').flush({ status: 'Alive' });
    expect(store.selectSnapshot(ScaffoldState.view).loading).toBe(false);
  });
});
