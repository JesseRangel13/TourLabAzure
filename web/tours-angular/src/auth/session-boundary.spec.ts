import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideStore, Store } from '@ngxs/store';
import { AuthState, UserSessionChanged } from './auth.state';
import { sessionExpiryInterceptor } from './session.interceptor';
import { ScaffoldState } from '../scaffold.state';
import { CatalogState } from '../catalog/catalog.state';
import { ImagesState } from '../catalog/images.state';
import { ReservationsState } from '../reservations/reservations.state';
import { EffectsState } from '../reservations/effects.state';
import { AdminState } from '../admin/admin.state';
import { DashboardState, LoadDashboardPage } from '../dashboard/dashboard.state';
import { ReceiptsState, LoadReceipts } from '../receipts/receipts.state';

describe('Complete application session boundary',()=>{
  let store:Store,http:HttpTestingController,initial:Record<string,any>;
  const features=['scaffold','catalog','images','reservations','effects','admin','dashboard','receipts'];
  beforeEach(()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(withInterceptors([sessionExpiryInterceptor])),
      provideHttpClientTesting(),provideRouter([]),provideStore([AuthState,ScaffoldState,CatalogState,
        ImagesState,ReservationsState,EffectsState,AdminState,DashboardState,ReceiptsState])]});
    store=TestBed.inject(Store);http=TestBed.inject(HttpTestingController);initial=structuredClone(store.snapshot());
    const dirty=structuredClone(initial);
    for(const key of features)dirty[key]={...dirty[key],error:'previous-owner-private-error',loading:true};
    dirty.auth={user:{userId:'old-owner',userName:'Alice',role:'Tourist'},initialized:true,loading:false,error:null};
    store.reset(dirty);
  });
  afterEach(()=>http.verify());
  for(const boundary of ['account change','real interceptor 401'])it(`resets every feature on ${boundary} and ignores an outstanding receipt response`,()=>{
    store.dispatch(new LoadReceipts('old-reservation')).subscribe();
    const pending=http.expectOne('/api/notifications/v1/receipts/reservation/old-reservation');
    if(boundary==='account change')store.dispatch(new UserSessionChanged()).subscribe();
    else{
      store.dispatch(new LoadDashboardPage()).subscribe({error:()=>{}});
      http.expectOne(request=>request.url==='/api/reservations/v1/dashboard/bookings')
        .flush({},{status:401,statusText:'Unauthorized'});
      expect(store.selectSnapshot(AuthState.view).user).toBeNull();
    }
    pending.flush([{id:'old-receipt',reservationId:'old-reservation',status:'Ready',simulated:true}]);
    for(const key of features)expect(store.snapshot()[key],key).toEqual(initial[key]);
    expect(JSON.stringify(store.snapshot())).not.toContain('previous-owner-private-error');
    expect(JSON.stringify(store.snapshot())).not.toContain('old-receipt');
  });
});
