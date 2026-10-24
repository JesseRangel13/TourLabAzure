import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideStore, Store } from '@ngxs/store';
import { AuthState, UserSessionChanged } from '../auth/auth.state';
import { sessionExpiryInterceptor } from '../auth/session.interceptor';
import { DashboardState, LoadDashboardPage, LoadDashboardBooking, RebuildDashboard, BookingDocument } from './dashboard.state';
import { DashboardPageComponent } from './dashboard.pages';
describe('Cosmos dashboard shared state',()=>{
  let store:Store,http:HttpTestingController;
  const item:BookingDocument={id:'r',reservationId:'r',userId:'alice',tourNameSnapshot:'Accepted tour',
    departureUtc:'2030-01-01T00:00:00.0000000Z',participants:2,amountCents:24690,currency:'MXN',status:'Confirmed',businessVersion:4,projectedAtUtc:'2026-10-24T00:00:00Z'};
  const page={items:[item],cursor:'opaque-next',eventuallyConsistent:true};
  beforeEach(()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(withInterceptors([sessionExpiryInterceptor])),provideHttpClientTesting(),
      provideStore([AuthState,DashboardState]),provideRouter([])]});
    store=TestBed.inject(Store);http=TestBed.inject(HttpTestingController);
  });
  afterEach(()=>http.verify());
  function requestPage(){return http.expectOne(r=>r.url==='/api/reservations/v1/dashboard/bookings');}
  it('uses protected cursors without an owner parameter and exposes eventual consistency',()=>{
    store.dispatch(new LoadDashboardPage('opaque-current')).subscribe();
    const request=requestPage();expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('cursor')).toBe('opaque-current');expect(request.request.params.has('userId')).toBe(false);
    request.flush(page);expect(store.selectSnapshot(DashboardState.view).page).toEqual(page);
    expect(store.selectSnapshot(DashboardState.view).loading).toBe(false);
  });
  it('cancels older queries and ignores a pending response across account change',()=>{
    store.dispatch(new LoadDashboardPage()).subscribe();const old=requestPage();
    store.dispatch(new LoadDashboardPage('next')).subscribe();expect(old.cancelled).toBe(true);const pending=requestPage();
    store.dispatch(new UserSessionChanged()).subscribe();pending.flush(page);
    expect(store.selectSnapshot(DashboardState.view).page).toBeNull();expect(store.selectSnapshot(DashboardState.view).loading).toBe(false);
  });
  it('reports Cosmos outage and trace id without substituting SQL bookings',()=>{
    store.dispatch(new LoadDashboardPage()).subscribe({error:()=>{}});
    requestPage().flush({traceId:'safe-trace'},{status:503,statusText:'Unavailable'});
    const state=store.selectSnapshot(DashboardState.view);
    expect(state.error).toContain('Cosmos dashboard is unavailable');expect(state.error).toContain('safe-trace');expect(state.page).toBeNull();
    http.expectNone('/api/reservations/v1/reservations');expect(state.loading).toBe(false);
    store.dispatch(new LoadDashboardPage()).subscribe();requestPage().flush(page);expect(store.selectSnapshot(DashboardState.view).error).toBeNull();
  });
  it('clears private state on 401 and classifies expired cursor failure',()=>{
    store.dispatch(new LoadDashboardPage()).subscribe();requestPage().flush(page);
    store.dispatch(new LoadDashboardPage('expired')).subscribe({error:()=>{}});
    requestPage().flush({},{status:400,statusText:'Bad cursor'});expect(store.selectSnapshot(DashboardState.view).error).toContain('first page');
    store.dispatch(new LoadDashboardBooking('r')).subscribe({error:()=>{}});
    http.expectOne('/api/reservations/v1/dashboard/bookings/r').flush({},{status:401,statusText:'Unauthorized'});
    expect(store.selectSnapshot(DashboardState.view).page).toBeNull();expect(store.selectSnapshot(DashboardState.view).detail).toBeNull();
  });
  it('ignores stale point reads on navigation and account changes',()=>{
    store.dispatch(new LoadDashboardBooking('old')).subscribe();const old=http.expectOne('/api/reservations/v1/dashboard/bookings/old');
    store.dispatch(new LoadDashboardBooking('r')).subscribe();expect(old.cancelled).toBe(true);const pending=http.expectOne('/api/reservations/v1/dashboard/bookings/r');
    store.dispatch(new UserSessionChanged()).subscribe();pending.flush(item);
    expect(store.selectSnapshot(DashboardState.view).detail).toBeNull();
  });
  it('serializes bounded rebuild batches, reuses continuation and has no automatic retries',()=>{
    store.dispatch(new RebuildDashboard(true)).subscribe();const first=http.expectOne('/api/reservations/v1/admin/dashboard/rebuild');
    expect(first.request.body).toEqual({batchSize:25,cursor:null});
    let denied=false;store.dispatch(new RebuildDashboard()).subscribe({error:()=>denied=true});expect(denied).toBe(true);
    first.flush({enqueued:25,cursor:'resume',run:'run',eventuallyConsistent:true});
    store.dispatch(new RebuildDashboard()).subscribe({error:()=>{}});
    const second=http.expectOne('/api/reservations/v1/admin/dashboard/rebuild');expect(second.request.body.cursor).toBe('resume');
    second.flush({traceId:'rebuild-trace'},{status:503,statusText:'Unavailable'});
    expect(store.selectSnapshot(DashboardState.view).rebuilding).toBe(false);
    expect(store.selectSnapshot(DashboardState.view).rebuild?.cursor).toBe('resume');
  });
  it('cannot repopulate rebuild state after logout',()=>{
    store.dispatch(new RebuildDashboard(true)).subscribe();const pending=http.expectOne('/api/reservations/v1/admin/dashboard/rebuild');
    store.dispatch(new UserSessionChanged()).subscribe();pending.flush({enqueued:2,cursor:'old-account',run:'run',eventuallyConsistent:true});
    expect(store.selectSnapshot(DashboardState.view).rebuild).toBeNull();
  });
  it('renders eventual consistency and authoritative links while hiding Admin controls',()=>{
    const fixture=TestBed.createComponent(DashboardPageComponent);fixture.detectChanges();requestPage().flush(page);fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Eventually consistent');
    expect(fixture.nativeElement.textContent).toContain('Accepted tour');
    expect(fixture.nativeElement.querySelector('a[href="/reservations/r"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Start projection rebuild');
    fixture.destroy();
  });
  it('ignores a list failure after navigation has loaded a point read',()=>{
    store.dispatch(new LoadDashboardPage()).subscribe({error:()=>{}});const old=requestPage();
    store.dispatch(new LoadDashboardBooking('r')).subscribe();
    http.expectOne('/api/reservations/v1/dashboard/bookings/r').flush(item);
    old.flush({},{status:503,statusText:'Old page outage'});
    expect(store.selectSnapshot(DashboardState.view).detail).toEqual(item);
    expect(store.selectSnapshot(DashboardState.view).error).toBeNull();
    expect(store.selectSnapshot(DashboardState.view).loading).toBe(false);
  });
  it('renders Admin recovery and advances only the explicit next bounded batch',()=>{
    store.reset({...store.snapshot(),auth:{user:{userId:'admin',userName:'Admin',role:'Admin'},loading:false,error:null,initialized:true}});
    const fixture=TestBed.createComponent(DashboardPageComponent);fixture.detectChanges();requestPage().flush(page);fixture.detectChanges();
    const buttons=()=>Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    buttons().find(b=>b.textContent?.trim()==='Start projection rebuild')!.click();
    http.expectOne('/api/reservations/v1/admin/dashboard/rebuild').flush({enqueued:25,cursor:'next-batch',run:'run',eventuallyConsistent:true});
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('25 snapshots enqueued');
    buttons().find(b=>b.textContent?.trim()==='Continue rebuild batch')!.click();
    const next=http.expectOne('/api/reservations/v1/admin/dashboard/rebuild');expect(next.request.body.cursor).toBe('next-batch');
    next.flush({enqueued:1,cursor:null,run:'run',eventuallyConsistent:true});fixture.detectChanges();
    expect(buttons().find(b=>b.textContent?.trim()==='Continue rebuild batch')!.disabled).toBe(true);
    fixture.destroy();
  });});
