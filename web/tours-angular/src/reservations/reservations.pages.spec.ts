import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { provideStore } from '@ngxs/store';
import { BookingForm, ReservationsPage } from './reservations.pages';
import { ReservationsState } from './reservations.state';
describe('booking controls', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [BookingForm, ReservationsPage], providers: [
    provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideStore([ReservationsState])
  ] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function form() {
    const fixture = TestBed.createComponent(BookingForm);
    fixture.componentRef.setInput('session', { id: 'session', tourId: 'tour', startsAtUtc: '2030-01-01T00:00:00Z',
      unitAmountMinor: 12345, currency: 'MXN', capacity: 6, availableSeats: 6, version: 1, priceVersion: 1 });
    fixture.detectChanges(); return fixture;
  }
  it('accepts only integer participants from one to six', () => {
    const fixture = form();
    for (const value of [0, 7, 1.5]) {
      fixture.componentInstance.form.controls.participants.setValue(value);
      expect(fixture.componentInstance.form.invalid).toBe(true);
      fixture.componentInstance.submit();
    }
    TestBed.inject(HttpTestingController).expectNone('/api/reservations/v1/reservations');
    fixture.componentInstance.form.controls.participants.setValue(6);
    expect(fixture.componentInstance.form.valid).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Accepted price');
    expect(fixture.nativeElement.textContent).toContain('simulated');
  });
  it('keeps the same key for unchanged explicit retries and creates a new intention only for changed input', () => {
    const fixture = form(), http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.submit();
    const first = http.expectOne('/api/reservations/v1/reservations');
    const key = first.request.headers.get('Idempotency-Key');
    first.flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.componentInstance.submit();
    const retry = http.expectOne('/api/reservations/v1/reservations');
    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    retry.flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.componentInstance.form.controls.participants.setValue(2);
    fixture.componentInstance.submit();
    const changed = http.expectOne('/api/reservations/v1/reservations');
    expect(changed.request.headers.get('Idempotency-Key')).not.toBe(key);
    changed.flush({}, { status: 409, statusText: 'Conflict' });
  });
  it('renders an empty authoritative booking list with a route to tours', async () => {
    const fixture = TestBed.createComponent(ReservationsPage);
    TestBed.inject(HttpTestingController).expectOne(req => req.url === '/api/reservations/v1/reservations').flush({ items: [], page: 1, pageSize: 10, total: 0 });
    await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No bookings yet');
  });
});