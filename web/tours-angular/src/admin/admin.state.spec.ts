import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideStore, Store } from '@ngxs/store';
import { AuthState, UserSessionChanged } from '../auth/auth.state';
import { sessionExpiryInterceptor } from '../auth/session.interceptor';
import { AdminState, ChangeFault, InspectDlq, LoadFault, LoadOperations, LoadSagas, RunAdminCommand } from './admin.state';
const diagnostics={service:'catalog',pendingOutboxCount:3,oldestPendingOutboxAgeSeconds:7,ignoredDuplicates:1,states:{},workers:[],outbox:[],inbox:[],audit:[]};
describe('Admin operations state',()=>{
 let store:Store,http:HttpTestingController;
 beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(withInterceptors([sessionExpiryInterceptor])),provideHttpClientTesting(),provideStore([AuthState,AdminState])]});store=TestBed.inject(Store);http=TestBed.inject(HttpTestingController);});
 afterEach(()=>http.verify());
 it('uses typed bounded diagnostics and cancels an old selected service',()=>{
  store.dispatch(new LoadOperations('catalog')).subscribe();const old=http.expectOne(r=>r.url.includes('/catalog/'));
  store.dispatch(new LoadOperations('payments',2)).subscribe();expect(old.cancelled).toBe(true);
  const current=http.expectOne(r=>r.url.includes('/payments/'));expect(current.request.params.get('pageSize')).toBe('10');expect(current.request.params.get('page')).toBe('2');
  current.flush({...diagnostics,service:'payments'});expect(store.selectSnapshot(AdminState.view).diagnostics?.service).toBe('payments');expect(store.selectSnapshot(AdminState.view).loading).toBe(false);
 });
 it('keeps SQL diagnostics visible when separate broker inspection fails',()=>{
  store.dispatch(new LoadOperations('catalog')).subscribe();http.expectOne(r=>r.url.endsWith('diagnostics')).flush(diagnostics);
  store.dispatch(new InspectDlq('catalog','events',17)).subscribe({error:()=>{}});
  const request=http.expectOne(r=>r.url.endsWith('dead-letters'));expect(request.request.params.get('source')).toBe('events');expect(request.request.params.get('fromSequence')).toBe('17');
  request.flush({},{status:503,statusText:'Unavailable'});const state=store.selectSnapshot(AdminState.view);
  expect(state.diagnostics?.pendingOutboxCount).toBe(3);expect(state.brokerError).toBeTruthy();expect(state.brokerLoading).toBe(false);
 });
 it('ignores stale DLQ responses and clears inspection loading when service changes',()=>{
  store.dispatch(new InspectDlq('catalog','commands')).subscribe();const old=http.expectOne(r=>r.url.endsWith('dead-letters'));
  store.dispatch(new LoadOperations('payments')).subscribe();http.expectOne(r=>r.url.endsWith('diagnostics')).flush({...diagnostics,service:'payments'});
  old.flush([{id:'old',source:'commands',sequenceNumber:1,replayable:true}]);expect(store.selectSnapshot(AdminState.view).deadLetters).toEqual([]);expect(store.selectSnapshot(AdminState.view).brokerLoading).toBe(false);
 });
 it('serializes mutations, never automatically retries and clears loading after failure',()=>{
  store.dispatch(new ChangeFault('catalog','QuoteTimeout',3)).subscribe({error:()=>{}});
  const first=http.expectOne(r=>r.url.endsWith('faults'));expect(first.request.body).toEqual({mode:'QuoteTimeout',occurrences:3});
  let rejected=false;store.dispatch(new RunAdminCommand({kind:'reconcile',id:'id'})).subscribe({error:()=>rejected=true});expect(rejected).toBe(true);
  first.flush({},{status:503,statusText:'Unavailable'});expect(store.selectSnapshot(AdminState.view).mutating).toBe(false);
  http.expectNone(r=>r.url.includes('reconcile'));
 });
 it('does not attach an old selected-service fault result to a new selection',()=>{
  store.dispatch(new ChangeFault('catalog','QuoteTimeout',1)).subscribe();const old=http.expectOne(r=>r.method==='PUT');
  store.dispatch(new LoadFault('payments')).subscribe();http.expectOne(r=>r.method==='GET').flush({mode:'Success',remaining:0});
  old.flush({mode:'QuoteTimeout',remaining:1});expect(store.selectSnapshot(AdminState.view).fault?.mode).toBe('Success');
 });
 it('resolves only the fictional provider and requires explicit separate reconciliation',()=>{
  store.dispatch(new RunAdminCommand({kind:'resolve',id:'stable-operation',outcome:'NotCharged'})).subscribe();
  const request=http.expectOne('/api/payments/v1/admin/fake-provider/stable-operation/resolve');expect(request.request.body).toEqual({outcome:'NotCharged'});
  request.flush({},{status:202,statusText:'Accepted'});http.expectNone(r=>r.url.endsWith('reconcile'));expect(store.selectSnapshot(AdminState.view).outcome).toContain('accepted');
 });
 it('preserves inspection ID on replay and exposes conflict without automatic resend',()=>{
  store.dispatch(new RunAdminCommand({kind:'replay',id:'inspection',service:'reservations'})).subscribe({error:()=>{}});
  http.expectOne('/api/reservations/v1/admin/dead-letters/inspection/replay').flush({},{status:409,statusText:'Conflict'});
  expect(store.selectSnapshot(AdminState.view).error).toContain('Refresh');expect(store.selectSnapshot(AdminState.view).mutating).toBe(false);
 });
 it('clears all Admin data on 401 and ignores late accepted results from another account',()=>{
  store.dispatch(new RunAdminCommand({kind:'retry',id:'saga'})).subscribe();const old=http.expectOne(r=>r.url.endsWith('retry-compensation'));
  store.dispatch(new UserSessionChanged()).subscribe();old.flush({},{status:202,statusText:'Accepted'});expect(store.selectSnapshot(AdminState.view).outcome).toBeNull();
  store.dispatch(new LoadSagas('ManualReview',2)).subscribe({error:()=>{}});
  const query=http.expectOne(r=>r.url.endsWith('sagas'));expect(query.request.params.get('state')).toBe('ManualReview');expect(query.request.params.get('page')).toBe('2');
  query.flush({},{status:401,statusText:'Unauthorized'});expect(store.selectSnapshot(AdminState.view).sagas).toEqual([]);expect(store.selectSnapshot(AdminState.view).loading).toBe(false);
 });
 it('reports backend Admin denial and resets loading',()=>{
  store.dispatch(new LoadFault('catalog')).subscribe({error:()=>{}});
  http.expectOne(r=>r.url.endsWith('faults')).flush({},{status:403,statusText:'Forbidden'});expect(store.selectSnapshot(AdminState.view).error).toContain('Admin');expect(store.selectSnapshot(AdminState.view).loading).toBe(false);
 });
});
