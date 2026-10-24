import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideStore, Store } from '@ngxs/store';
import { AuthState, ResetSession, UserSessionChanged } from '../auth/auth.state';
import { sessionExpiryInterceptor } from '../auth/session.interceptor';
import { LoadEffects, LoadReservation, LoadReservations, ReservationsState, SubmitBooking } from './reservations.state';
describe('authoritative booking state', () => {
  let store: Store, http: HttpTestingController;
  const input = { sessionId: 'session', participants: 2, expectedUnitAmountMinor: 12345, expectedCurrency: 'MXN' };
  const accepted = { reservationId: 'booking', sagaId: 'saga', status: 'AwaitingAvailability' };
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([sessionExpiryInterceptor])), provideHttpClientTesting(), provideStore([AuthState, ReservationsState])] });
    store = TestBed.inject(Store); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('sends a typed accepted-price intention with its stable retry key and awaits durable 202', () => {
    store.dispatch(new SubmitBooking(input, 'stable-key')).subscribe();
    const request = http.expectOne('/api/reservations/v1/reservations');
    expect(request.request.headers.get('Idempotency-Key')).toBe('stable-key');
    expect(request.request.body).toEqual(input);
    expect(store.selectSnapshot(ReservationsState.view).creating).toBe(true);
    request.flush(accepted, { status: 202, statusText: 'Accepted' });
    expect(store.selectSnapshot(ReservationsState.view).accepted).toEqual(accepted);
    expect(store.selectSnapshot(ReservationsState.view).creating).toBe(false);
  });
  it('does not automatically retry a failed mutation or create another key', () => {
    let failed = false;
    store.dispatch(new SubmitBooking(input, 'stable-key')).subscribe({ error: () => failed = true });
    http.expectOne('/api/reservations/v1/reservations').flush({}, { status: 503, statusText: 'Unavailable' });
    expect(failed).toBe(true);
    expect(store.selectSnapshot(ReservationsState.view).error).toContain('Retry unchanged');
    expect(store.selectSnapshot(ReservationsState.view).creating).toBe(false);
    store.dispatch(new SubmitBooking(input, 'stable-key')).subscribe();
    const retry = http.expectOne('/api/reservations/v1/reservations');
    expect(retry.request.headers.get('Idempotency-Key')).toBe('stable-key');
    retry.flush(accepted, { status: 202, statusText: 'Accepted' });
  });
  it('serializes duplicate button submissions and clears uncertain results across account changes', () => {
    store.dispatch(new SubmitBooking(input, 'first')).subscribe();
    const old = http.expectOne('/api/reservations/v1/reservations');
    let rejected = false;
    store.dispatch(new SubmitBooking(input, 'second')).subscribe({ error: () => rejected = true });
    expect(rejected).toBe(true);
    store.dispatch(new UserSessionChanged()).subscribe();
    old.flush(accepted);
    expect(store.selectSnapshot(ReservationsState.view).accepted).toBeNull();
    expect(store.selectSnapshot(ReservationsState.view).creating).toBe(false);
  });
  it('cancels stale list loads and does not accept an old user response', () => {
    store.dispatch(new LoadReservations(1)).subscribe();
    const old = http.expectOne(req => req.params.get('page') === '1');
    store.dispatch(new LoadReservations(2)).subscribe();
    expect(old.cancelled).toBe(true);
    const current = http.expectOne(req => req.params.get('page') === '2');
    store.dispatch(new ResetSession()).subscribe();
    current.flush({ items: [{ id: 'other-user-data' }], page: 2, pageSize: 10, total: 11 });
    expect(store.selectSnapshot(ReservationsState.bookings)).toEqual([]);
    expect(store.selectSnapshot(ReservationsState.view).loading).toBe(false);
  });
  it('reports a missing owned resource as unavailable and clears loading', () => {
    store.dispatch(new LoadReservation('hidden')).subscribe({ error: () => {} });
    const detail = http.expectOne('/api/reservations/v1/reservations/hidden');
    const timeline = http.expectOne('/api/reservations/v1/reservations/hidden/timeline');
    detail.flush({}, { status: 404, statusText: 'Not Found' });
    expect(timeline.cancelled).toBe(true);
    expect(store.selectSnapshot(ReservationsState.view).detail).toBeNull();
    expect(store.selectSnapshot(ReservationsState.view).loading).toBe(false);
    expect(store.selectSnapshot(ReservationsState.view).error).toContain('unavailable');
  });
  it('keeps authoritative details visible if a simulated-effect service is unavailable', () => {
    store.dispatch(new LoadReservation('booking')).subscribe();
    http.expectOne('/api/reservations/v1/reservations/booking').flush({ id: 'booking', status: 'Confirmed' });
    http.expectOne('/api/reservations/v1/reservations/booking/timeline').flush([]);
    store.dispatch(new LoadEffects('booking')).subscribe({ error: () => {} });
    const payment = http.expectOne(req => req.url === '/api/payments/v1/payments');
    const notifications = http.expectOne(req => req.url === '/api/notifications/v1/notifications');
    payment.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(notifications.cancelled).toBe(true);
    expect(store.selectSnapshot(ReservationsState.view).detail?.status).toBe('Confirmed');
    expect(store.selectSnapshot(ReservationsState.view).effectsError).toContain('unavailable');
    expect(store.selectSnapshot(ReservationsState.view).effectsLoading).toBe(false);
  });
  it('clears effect data on 401 and preserves no stale account results', () => {
    store.dispatch(new LoadEffects()).subscribe({ error: () => {} });
    const payment = http.expectOne(req => req.url === '/api/payments/v1/payments');
    const notifications = http.expectOne(req => req.url === '/api/notifications/v1/notifications');
    payment.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(notifications.cancelled).toBe(true);
    expect(store.selectSnapshot(ReservationsState.view).payments).toEqual([]);
    expect(store.selectSnapshot(ReservationsState.view).effectsLoading).toBe(false);
  });
});