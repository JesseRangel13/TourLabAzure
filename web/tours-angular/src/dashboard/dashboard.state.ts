import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Action, Selector, State, StateContext } from '@ngxs/store';
import { catchError, finalize, tap, throwError } from 'rxjs';
import { UserSessionChanged } from '../auth/auth.state';
export interface BookingDocument {
  id:string;userId:string;reservationId:string;tourNameSnapshot:string;departureUtc:string;participants:number;
  amountCents:number;currency:'MXN';status:string;businessVersion:number;projectedAtUtc:string;
}
export interface DashboardPage {items:BookingDocument[];cursor:string|null;eventuallyConsistent:boolean;}
export interface RebuildResult {enqueued:number;cursor:string|null;run:string;eventuallyConsistent:boolean;}
@Injectable({providedIn:'root'})
export class DashboardApi {
  private readonly http=inject(HttpClient);
  page(cursor:string|null){let params=new HttpParams().set('pageSize',10);if(cursor)params=params.set('cursor',cursor);
    return this.http.get<DashboardPage>('/api/reservations/v1/dashboard/bookings',{params});}
  detail(id:string){return this.http.get<BookingDocument>('/api/reservations/v1/dashboard/bookings/'+encodeURIComponent(id));}
  rebuild(cursor:string|null){return this.http.post<RebuildResult>('/api/reservations/v1/admin/dashboard/rebuild',{batchSize:25,cursor});}
}
export class LoadDashboardPage {static readonly type='[Dashboard] Page';constructor(readonly cursor:string|null=null){}}
export class LoadDashboardBooking {static readonly type='[Dashboard] Detail';constructor(readonly id:string){}}
export class RebuildDashboard {static readonly type='[Dashboard] Rebuild';constructor(readonly restart=false){}}
interface DashboardModel {page:DashboardPage|null;detail:BookingDocument|null;loading:boolean;detailLoading:boolean;
  rebuilding:boolean;rebuild:RebuildResult|null;error:string|null;rebuildError:string|null;}
const defaults=():DashboardModel=>({page:null,detail:null,loading:false,detailLoading:false,rebuilding:false,rebuild:null,error:null,rebuildError:null});
@State<DashboardModel>({name:'dashboard',defaults:defaults()})
@Injectable()
export class DashboardState {
  private readonly api=inject(DashboardApi);private generation=0;private listRequest=0;private detailRequest=0;
  @Selector() static view(s:DashboardModel){return s;}
  @Action(UserSessionChanged) reset(ctx:StateContext<DashboardModel>){++this.generation;++this.listRequest;++this.detailRequest;ctx.setState(defaults());}
  @Action(LoadDashboardPage,{cancelUncompleted:true})
  page(ctx:StateContext<DashboardModel>,a:LoadDashboardPage){
    const g=this.generation,r=++this.listRequest,current=()=>g===this.generation&&r===this.listRequest;
    ++this.detailRequest;
    ctx.patchState({page:null,detail:null,loading:true,detailLoading:false,error:null});
    return this.api.page(a.cursor).pipe(tap(page=>{if(current())ctx.patchState({page});}),
      catchError((e:unknown)=>{if(current())ctx.patchState({error:message(e)});return throwError(()=>e);}),
      finalize(()=>{if(current())ctx.patchState({loading:false});}));
  }
  @Action(LoadDashboardBooking,{cancelUncompleted:true})
  detail(ctx:StateContext<DashboardModel>,a:LoadDashboardBooking){
    const g=this.generation,r=++this.detailRequest,current=()=>g===this.generation&&r===this.detailRequest;
    ++this.listRequest;
    ctx.patchState({detail:null,detailLoading:true,loading:false,error:null});
    return this.api.detail(a.id).pipe(tap(detail=>{if(current())ctx.patchState({detail});}),
      catchError((e:unknown)=>{if(current())ctx.patchState({error:message(e)});return throwError(()=>e);}),
      finalize(()=>{if(current())ctx.patchState({detailLoading:false});}));
  }
  @Action(RebuildDashboard)
  rebuild(ctx:StateContext<DashboardModel>,a:RebuildDashboard){
    if(ctx.getState().rebuilding)return throwError(()=>new Error('A rebuild batch is already in progress.'));
    const g=this.generation,cursor=a.restart?null:ctx.getState().rebuild?.cursor??null;
    ctx.patchState({rebuilding:true,rebuildError:null});
    return this.api.rebuild(cursor).pipe(tap(rebuild=>{if(g===this.generation)ctx.patchState({rebuild});}),
      catchError((e:unknown)=>{if(g===this.generation)ctx.patchState({rebuildError:message(e)});return throwError(()=>e);}),
      finalize(()=>{if(g===this.generation)ctx.patchState({rebuilding:false});}));
  }
}
function message(error:unknown):string {
  if(error instanceof HttpErrorResponse){
    if(error.status===400)return 'This page cursor is invalid or expired. Start again from the first page.';
    if(error.status===404)return 'This projected booking is not available for your account. Use the authoritative booking list.';
    if(error.status===403)return 'Administrator access is required.';
    if(error.status===401)return 'Your session expired. Sign in again.';
    const trace=typeof error.error?.traceId==='string'?error.error.traceId.slice(0,100):null;
    return 'Cosmos dashboard is unavailable. Authoritative booking actions remain available.'+(trace?' Trace: '+trace:'');
  }
  return 'Cosmos dashboard is unavailable. Open the authoritative booking list or explicitly retry.';
}
