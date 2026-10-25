import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Action, Selector, State, StateContext } from '@ngxs/store';
import { catchError, finalize, tap, throwError } from 'rxjs';
import { UserSessionChanged } from '../auth/auth.state';

export interface BookingReceipt {
  id:string; reservationId:string; sourceBusinessVersion:number; status:'Pending'|'Ready'|'Conflict';
  confirmedAtUtc:string; readyAtUtc:string|null; errorCode:string|null; simulated:true;
}
@Injectable({providedIn:'root'})
export class ReceiptsApi {
  private readonly http=inject(HttpClient);
  list(reservation:string){return this.http.get<BookingReceipt[]>('/api/notifications/v1/receipts/reservation/'+encodeURIComponent(reservation));}
  download(id:string){return this.http.get('/api/notifications/v1/receipts/'+encodeURIComponent(id)+'/content',{responseType:'blob'});}
}
@Injectable({providedIn:'root'})
export class ReceiptDownloads {
  save(content:Blob,reservation:string){
    const url=URL.createObjectURL(content);
    try {
      const link=document.createElement('a');link.href=url;
      link.download='simulated-confirmation-'+reservation+'.txt';link.click();
    } finally {URL.revokeObjectURL(url);}
  }
}
export class LoadReceipts {static readonly type='[Receipts] Load';constructor(readonly reservation:string){}}
export class DownloadReceipt {static readonly type='[Receipts] Download';constructor(readonly id:string){}}
interface ReceiptStateModel {reservation:string;items:BookingReceipt[];loading:boolean;downloading:boolean;error:string|null;}
const defaults=():ReceiptStateModel=>({reservation:'',items:[],loading:false,downloading:false,error:null});
@State<ReceiptStateModel>({name:'receipts',defaults:defaults()})
@Injectable()
export class ReceiptsState {
  private readonly api=inject(ReceiptsApi);private readonly downloads=inject(ReceiptDownloads);
  private generation=0;private request=0;
  @Selector() static view(s:ReceiptStateModel){return s;}
  @Action(UserSessionChanged) reset(ctx:StateContext<ReceiptStateModel>){++this.generation;++this.request;ctx.setState(defaults());}
  @Action(LoadReceipts,{cancelUncompleted:true})
  load(ctx:StateContext<ReceiptStateModel>,a:LoadReceipts){
    const g=this.generation,r=++this.request,current=()=>g===this.generation&&r===this.request;
    ctx.patchState({reservation:a.reservation,items:[],loading:true,error:null});
    return this.api.list(a.reservation).pipe(tap(items=>{if(current())ctx.patchState({items});}),
      catchError((e:unknown)=>{
        if(current()) {
          if(e instanceof HttpErrorResponse&&e.status===404)ctx.patchState({items:[]});
          else ctx.patchState({error:message(e)});
        }
        if(e instanceof HttpErrorResponse&&e.status===404)return [];
        return throwError(()=>e);
      }),finalize(()=>{if(current())ctx.patchState({loading:false});}));
  }
  @Action(DownloadReceipt,{cancelUncompleted:true})
  download(ctx:StateContext<ReceiptStateModel>,a:DownloadReceipt){
    const item=ctx.getState().items.find(x=>x.id===a.id&&x.status==='Ready');
    if(!item||ctx.getState().downloading)return throwError(()=>new Error('Receipt is not ready.'));
    const g=this.generation,reservation=ctx.getState().reservation;
    const current=()=>g===this.generation&&ctx.getState().reservation===reservation;
    ctx.patchState({downloading:true,error:null});
    return this.api.download(a.id).pipe(tap(content=>{if(current())this.downloads.save(content,item.reservationId);}),
      catchError((e:unknown)=>{if(current())ctx.patchState({error:message(e)});return throwError(()=>e);}),
      finalize(()=>{if(g===this.generation)ctx.patchState({downloading:false});}));
  }
}
function message(e:unknown){
  if(e instanceof HttpErrorResponse){
    if(e.status===401)return 'Your session expired. Sign in again.';
    if(e.status===404)return 'This confirmation is unavailable.';
    if(e.status===409)return 'The confirmation is not ready. Refresh its status.';
  }
  return 'Confirmation storage is unavailable. Booking and simulated notification delivery continue independently. Retry later.';
}