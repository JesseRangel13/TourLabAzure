import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideStore, Store } from '@ngxs/store';
import { AuthState } from '../auth/auth.state';
import { CatalogState } from './catalog.state';
import { CatalogListPage, CatalogDetailPage, SessionEditor } from './catalog.pages';

describe('Catalog controls', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [CatalogListPage, CatalogDetailPage, SessionEditor], providers: [
    provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideStore([AuthState, CatalogState])
  ] }));
  it('renders the list and hides Admin mutations for tourists', async () => {
    const fixture = TestBed.createComponent(CatalogListPage);
    TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/tours')).flush({ items: [], total: 0, page: 1, pageSize: 10 });
    await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No tours match');
    expect(fixture.nativeElement.textContent).not.toContain('Create tour');
  });
  it('shows reactive Admin fields and rejects empty tour names', async () => {
    const store = TestBed.inject(Store);
    store.reset({ ...store.snapshot(), auth: { user: { userId: 'admin', userName: 'Admin', role: 'Admin' }, initialized: true, loading: false, error: null } });
    const fixture = TestBed.createComponent(CatalogListPage);
    TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/tours')).flush({ items: [], total: 0, page: 1, pageSize: 10 });
    await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Create tour');
    fixture.componentInstance.create.controls.name.setValue('   ');
    expect(fixture.componentInstance.create.invalid).toBe(true);
    fixture.componentInstance.create.controls.name.setValue('Forest');
    expect(fixture.componentInstance.create.valid).toBe(true);
  });
  it('rejects fractional cents and negative or fractional capacity', async () => {
    const fixture = TestBed.createComponent(SessionEditor);
    fixture.componentRef.setInput('session', { id: 's', tourId: 't', startsAtUtc: '2030-01-01T00:00:00Z',
      unitAmountMinor: 100, currency: 'MXN', capacity: 1, availableSeats: 1, priceVersion: 1, version: 1 });
    await fixture.whenStable();
    fixture.componentInstance.form.setValue({ amount: 1.5, capacity: -1 });
    expect(fixture.componentInstance.form.invalid).toBe(true);
    fixture.componentInstance.form.setValue({ amount: 100, capacity: 1.5 });
    expect(fixture.componentInstance.form.invalid).toBe(true);
  });
});