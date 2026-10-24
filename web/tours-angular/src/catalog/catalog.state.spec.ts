import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideStore, Store } from '@ngxs/store';
import { CatalogState, LoadTours, LoadCatalogTour, SaveTour, ChangeSessionCapacity } from './catalog.state';
import { AuthState, ResetSession, UserSessionChanged } from '../auth/auth.state';
import { sessionExpiryInterceptor } from '../auth/session.interceptor';
import { Tour, Session } from './catalog.api';

describe('Catalog shared state', () => {
  let store: Store, http: HttpTestingController;
  const tour: Tour = { id: 'tour-a', name: 'River', description: '', active: true, version: 1 };
  const session: Session = { id: 's-a', tourId: 'tour-a', startsAtUtc: '2030-01-01T00:00:00Z',
    capacity: 4, availableSeats: 2, unitAmountMinor: 12345, currency: 'MXN', version: 2, priceVersion: 1 };
  const page = { items: [tour], page: 1, pageSize: 10, total: 1 };
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([sessionExpiryInterceptor])), provideHttpClientTesting(), provideStore([AuthState, CatalogState])
    ] });
    store = TestBed.inject(Store); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function loadDetail() {
    store.dispatch(new LoadCatalogTour(tour.id)).subscribe();
    http.expectOne('/api/catalog/v1/tours/tour-a').flush(tour);
    http.expectOne('/api/catalog/v1/tours/tour-a/sessions').flush([session]);
  }
  it('loads typed pages and exposes selectors while clearing loading', () => {
    store.dispatch(new LoadTours('river')).subscribe();
    expect(store.selectSnapshot(CatalogState.view).listLoading).toBe(true);
    const request = http.expectOne(req => req.url === '/api/catalog/v1/tours' && req.params.get('search') === 'river');
    expect(request.request.params.get('pageSize')).toBe('10');
    request.flush(page);
    expect(store.selectSnapshot(CatalogState.tours)).toEqual([tour]);
    expect(store.selectSnapshot(CatalogState.view).listLoading).toBe(false);
  });
  it('cancels an older filter so it cannot overwrite the new page', () => {
    store.dispatch(new LoadTours('old')).subscribe();
    const old = http.expectOne(req => req.params.get('search') === 'old');
    store.dispatch(new LoadTours('new')).subscribe();
    expect(old.cancelled).toBe(true);
    http.expectOne(req => req.params.get('search') === 'new').flush({ ...page, items: [] });
    expect(store.selectSnapshot(CatalogState.tours)).toEqual([]);
    expect(store.selectSnapshot(CatalogState.view).search).toBe('new');
  });
  it('ignores a pending response across an account boundary', () => {
    store.dispatch(new LoadTours()).subscribe();
    const pending = http.expectOne(req => req.url.endsWith('/tours'));
    store.dispatch(new UserSessionChanged()).subscribe();
    pending.flush(page);
    expect(store.selectSnapshot(CatalogState.view).page).toBeNull();
    expect(store.selectSnapshot(CatalogState.view).listLoading).toBe(false);
  });
  it('clears details on logout and 401 without persisting another user data', () => {
    loadDetail();
    store.dispatch(new ResetSession()).subscribe();
    expect(store.selectSnapshot(CatalogState.view).tour).toBeNull();
    store.dispatch(new LoadTours()).subscribe({ error: () => {} });
    http.expectOne(req => req.url.endsWith('/tours')).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(store.selectSnapshot(CatalogState.view).sessions).toEqual([]);
    expect(store.selectSnapshot(CatalogState.view).listLoading).toBe(false);
  });
  it('propagates unavailable failures and allows an explicit retry', () => {
    let failed = false;
    store.dispatch(new LoadTours()).subscribe({ error: () => failed = true });
    http.expectOne(req => req.url.endsWith('/tours')).flush({}, { status: 503, statusText: 'Unavailable' });
    expect(failed).toBe(true);
    expect(store.selectSnapshot(CatalogState.view).error).toContain('unavailable');
    expect(store.selectSnapshot(CatalogState.view).listLoading).toBe(false);
    store.dispatch(new LoadTours()).subscribe();
    http.expectOne(req => req.url.endsWith('/tours')).flush(page);
    expect(store.selectSnapshot(CatalogState.view).error).toBeNull();
  });
  it('keeps the expected version on mutation and classifies conflicts without retry', () => {
    loadDetail();
    let failed = false;
    store.dispatch(new ChangeSessionCapacity(session, 1)).subscribe({ error: () => failed = true });
    const request = http.expectOne('/api/catalog/v1/admin/sessions/s-a/capacity');
    expect(request.request.body).toEqual({ capacity: 1, expectedVersion: 2 });
    request.flush({ code: 'CapacityOccupied' }, { status: 409, statusText: 'Conflict' });
    expect(failed).toBe(true);
    expect(store.selectSnapshot(CatalogState.view).saving).toBe(false);
    expect(store.selectSnapshot(CatalogState.view).sessions).toEqual([session]);
    expect(store.selectSnapshot(CatalogState.view).error).toContain('Reload');
  });
  it('ignores an old mutation result after account change and serializes mutations', () => {
    store.dispatch(new SaveTour({ name: 'River', description: '' })).subscribe();
    const pending = http.expectOne('/api/catalog/v1/admin/tours');
    let rejected = false;
    store.dispatch(new SaveTour({ name: 'Second', description: '' })).subscribe({ error: () => rejected = true });
    expect(rejected).toBe(true);
    store.dispatch(new UserSessionChanged()).subscribe();
    pending.flush(tour);
    expect(store.selectSnapshot(CatalogState.view).tour).toBeNull();
    expect(store.selectSnapshot(CatalogState.view).saving).toBe(false);
  });
});