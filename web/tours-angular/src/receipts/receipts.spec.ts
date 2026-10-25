import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideStore, Store } from '@ngxs/store';
import { provideRouter } from '@angular/router';
import { AuthState, UserSessionChanged } from '../auth/auth.state';
import { sessionExpiryInterceptor } from '../auth/session.interceptor';
import { BookingReceipt, DownloadReceipt, LoadReceipts, ReceiptDownloads, ReceiptsState } from './receipts.state';
import { ReceiptsPanel } from './receipts.panel';
describe('Historical simulated confirmations',()=>{
  let store:Store,http:HttpTestingController;
  const save=vi.fn();
  const item:BookingReceipt={id:'job',reservationId:'r',sourceBusinessVersion:4,status:'Ready',
    confirmedAtUtc:'2026-10-24T00:00:00Z',readyAtUtc:'2026-10-24T00:01:00Z',errorCode:null,simulated:true};
  const list='/api/notifications/v1/receipts/reservation/r';
  beforeEach(()=>{
    save.mockReset();TestBed.configureTestingModule({providers:[provideHttpClient(withInterceptors([sessionExpiryInterceptor])),
      provideHttpClientTesting(),provideStore([AuthState,ReceiptsState]),provideRouter([]),
      {provide:ReceiptDownloads,useValue:{save}}]});
    store=TestBed.inject(Store);http=TestBed.inject(HttpTestingController);
  });
  afterEach(()=>http.verify());
  function load(){store.dispatch(new LoadReceipts('r')).subscribe();http.expectOne(list).flush([item]);}
  it('loads owner status without a client owner parameter and keeps file bytes out of state',()=>{
    load();store.dispatch(new DownloadReceipt('job')).subscribe();
    const request=http.expectOne('/api/notifications/v1/receipts/job/content');expect(request.request.responseType).toBe('blob');
    const content=new Blob(['SIMULATED']);request.flush(content);expect(save).toHaveBeenCalledWith(content,'r');
    expect(JSON.stringify(store.snapshot())).not.toContain('SIMULATED');expect(store.selectSnapshot(ReceiptsState.view).downloading).toBe(false);
  });
  it('cancels old loads and ignores responses after account change',()=>{
    store.dispatch(new LoadReceipts('old')).subscribe();const old=http.expectOne('/api/notifications/v1/receipts/reservation/old');
    store.dispatch(new LoadReceipts('r')).subscribe();expect(old.cancelled).toBe(true);const pending=http.expectOne(list);
    store.dispatch(new UserSessionChanged()).subscribe();pending.flush([item]);expect(store.selectSnapshot(ReceiptsState.view).items).toEqual([]);
  });
  it('does not trigger a download after logout',()=>{
    load();store.dispatch(new DownloadReceipt('job')).subscribe();const pending=http.expectOne('/api/notifications/v1/receipts/job/content');
    store.dispatch(new UserSessionChanged()).subscribe();pending.flush(new Blob(['private']));expect(save).not.toHaveBeenCalled();
  });
  it('shows absence for an unknown receipt and an explicit retryable storage failure',()=>{
    store.dispatch(new LoadReceipts('r')).subscribe();http.expectOne(list).flush({},{status:404,statusText:'Not Found'});
    expect(store.selectSnapshot(ReceiptsState.view).error).toBeNull();
    store.dispatch(new LoadReceipts('r')).subscribe({error:()=>{}});http.expectOne(list).flush({},{status:503,statusText:'Unavailable'});
    expect(store.selectSnapshot(ReceiptsState.view).error).toContain('continue independently');
  });
  it('clears confirmation state on 401 and blocks downloads of pending documents',()=>{
    store.dispatch(new LoadReceipts('r')).subscribe();http.expectOne(list).flush([{...item,status:'Pending'}]);
    let denied=false;store.dispatch(new DownloadReceipt('job')).subscribe({error:()=>denied=true});expect(denied).toBe(true);
    store.dispatch(new LoadReceipts('r')).subscribe({error:()=>{}});http.expectOne(list).flush({},{status:401,statusText:'Unauthorized'});
    expect(store.selectSnapshot(ReceiptsState.view).items).toEqual([]);
  });
  it('renders cancellation separately from a historical ready confirmation',()=>{
    const f=TestBed.createComponent(ReceiptsPanel);f.componentRef.setInput('reservation','r');f.componentRef.setInput('currentStatus','Cancelled');
    f.detectChanges();http.expectOne(list).flush([item]);f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Cancelled');expect(f.nativeElement.textContent).toContain('Historical educational document');
    expect(f.nativeElement.textContent).toContain('Download simulated confirmation');f.destroy();
  });
});