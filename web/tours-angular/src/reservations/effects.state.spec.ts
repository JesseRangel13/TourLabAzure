import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideStore, Store } from '@ngxs/store';
import { UserSessionChanged } from '../auth/auth.state';
import { EffectsState, LoadEffectDetail, LoadProjections } from './effects.state';
import { CancelReservation, LoadEffects, ReservationsState } from './reservations.state';
describe('owned effects and cancellation',()=>{
 let store:Store,http:HttpTestingController;
 beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),provideStore([EffectsState,ReservationsState])]});store=TestBed.inject(Store);http=TestBed.inject(HttpTestingController);});
 afterEach(()=>http.verify());
 it('loads owned payment/refund detail and clears it on 404 instead of retaining the previous owner data',()=>{
  store.dispatch(new LoadEffectDetail('payments','one')).subscribe();http.expectOne('/api/payments/v1/payments/one').flush({id:'one',status:'Succeeded',refund:{id:'refund',status:'Refunded'}});
  expect(store.selectSnapshot(EffectsState.view).payment?.refund?.status).toBe('Refunded');
  store.dispatch(new LoadEffectDetail('payments','other')).subscribe({error:()=>{}});
  http.expectOne('/api/payments/v1/payments/other').flush({},{status:404,statusText:'Not Found'});
  expect(store.selectSnapshot(EffectsState.view).payment).toBeNull();expect(store.selectSnapshot(EffectsState.view).loading).toBe(false);
 });
 it('cancels stale effect detail queries and resets notification data across accounts',()=>{
  store.dispatch(new LoadEffectDetail('notifications','old')).subscribe();const old=http.expectOne('/api/notifications/v1/notifications/old');
  store.dispatch(new LoadEffectDetail('notifications','new')).subscribe();expect(old.cancelled).toBe(true);
  const current=http.expectOne('/api/notifications/v1/notifications/new');store.dispatch(new UserSessionChanged()).subscribe();current.flush({id:'new',body:'private'});
  expect(store.selectSnapshot(EffectsState.view).notification).toBeNull();
 });
 it('labels projection failure without falling back to Catalog',()=>{
  store.dispatch(new LoadProjections(2)).subscribe({error:()=>{}});const request=http.expectOne(r=>r.url.endsWith('tour-sessions'));
  expect(request.request.params.get('page')).toBe('2');request.flush({},{status:503,statusText:'Unavailable'});
  expect(store.selectSnapshot(EffectsState.view).error).toContain('authoritative');http.expectNone(r=>r.url.startsWith('/api/catalog/'));
 });
 it('keeps cancellation retry key, propagates 409 and never automatically retries',()=>{
  store.dispatch(new CancelReservation('booking','same-key')).subscribe({error:()=>{}});
  const first=http.expectOne('/api/reservations/v1/reservations/booking/cancel');expect(first.request.headers.get('Idempotency-Key')).toBe('same-key');
  first.flush({},{status:409,statusText:'Conflict'});expect(store.selectSnapshot(ReservationsState.view).creating).toBe(false);expect(store.selectSnapshot(ReservationsState.view).error).toContain('cannot be cancelled');
  store.dispatch(new CancelReservation('booking','same-key')).subscribe();const second=http.expectOne('/api/reservations/v1/reservations/booking/cancel');expect(second.request.headers.get('Idempotency-Key')).toBe('same-key');second.flush({},{status:202,statusText:'Accepted'});
 });
 it('serializes cancellation and ignores a late error after account reset',()=>{
  store.dispatch(new CancelReservation('booking','key')).subscribe({error:()=>{}});const old=http.expectOne(r=>r.url.endsWith('cancel'));
  let rejected=false;store.dispatch(new CancelReservation('booking','other')).subscribe({error:()=>rejected=true});expect(rejected).toBe(true);
  store.dispatch(new UserSessionChanged()).subscribe();old.flush({},{status:503,statusText:'Unavailable'});
  expect(store.selectSnapshot(ReservationsState.view).error).toBeNull();expect(store.selectSnapshot(ReservationsState.view).creating).toBe(false);
 });
 it('loads personal payments independently from the notification API',()=>{
  store.dispatch(new LoadEffects(undefined,1,'payments')).subscribe();
  http.expectOne(r=>r.url==='/api/payments/v1/payments').flush([{id:'payment',status:'Succeeded'}]);
  http.expectNone(r=>r.url.startsWith('/api/notifications/'));expect(store.selectSnapshot(ReservationsState.view).payments.length).toBe(1);
 });
});
