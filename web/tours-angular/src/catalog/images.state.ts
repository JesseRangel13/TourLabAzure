import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Action, Selector, State, StateContext } from '@ngxs/store';
import { catchError, finalize, Observable, switchMap, tap, throwError } from 'rxjs';
import { UserSessionChanged } from '../auth/auth.state';
export interface TourImage {
  id: string; tourId: string; contentType: string; byteSize: number;
  status: 'Pending'|'Ready'|'DeletePending'|'Deleted'; version: number; url: string|null; attempts: number;
}
@Injectable({providedIn:'root'})
export class ImagesApi {
  private readonly http=inject(HttpClient);
  list(tour:string){return this.http.get<TourImage[]>(this.base(tour));}
  upload(tour:string,id:string,file:File){return this.http.put<TourImage>(this.admin(tour)+'/'+id,file);}
  delete(tour:string,id:string){return this.http.delete<void>(this.admin(tour)+'/'+id);}
  recover(tour:string,id:string){return this.http.post<{processed:boolean}>(this.admin(tour)+'/'+id+'/recover',{});}
  private base(tour:string){return '/api/catalog/v1/tours/'+encodeURIComponent(tour)+'/images';}
  private admin(tour:string){return '/api/catalog/v1/admin/tours/'+encodeURIComponent(tour)+'/images';}
}
export class LoadImages {static readonly type='[Images] Load';constructor(readonly tour:string){}}
export class UploadImage {static readonly type='[Images] Upload';constructor(readonly tour:string,readonly id:string,readonly file:File){}}
export class RemoveImage {static readonly type='[Images] Remove';constructor(readonly tour:string,readonly id:string){}}
export class RecoverImage {static readonly type='[Images] Recover';constructor(readonly tour:string,readonly id:string){}}
interface ImageStateModel {tour:string;items:TourImage[];loading:boolean;saving:boolean;error:string|null;}
const defaults=():ImageStateModel=>({tour:'',items:[],loading:false,saving:false,error:null});
@State<ImageStateModel>({name:'images',defaults:defaults()})
@Injectable()
export class ImagesState {
  private readonly api=inject(ImagesApi);private generation=0;private request=0;
  @Selector() static view(s:ImageStateModel){return s;}
  @Action(UserSessionChanged) reset(ctx:StateContext<ImageStateModel>){++this.generation;++this.request;ctx.setState(defaults());}
  @Action(LoadImages,{cancelUncompleted:true})
  load(ctx:StateContext<ImageStateModel>,a:LoadImages){
    const g=this.generation,r=++this.request,current=()=>g===this.generation&&r===this.request;
    ctx.patchState({tour:a.tour,items:[],loading:true,error:null});
    return this.api.list(a.tour).pipe(tap(items=>{if(current())ctx.patchState({items});}),
      catchError((e:unknown)=>{if(current())ctx.patchState({error:message(e)});return throwError(()=>e);}),
      finalize(()=>{if(current())ctx.patchState({loading:false});}));
  }
  @Action(UploadImage) upload(ctx:StateContext<ImageStateModel>,a:UploadImage){
    if(!['image/png','image/jpeg'].includes(a.file.type)||a.file.size>2097152||!a.file.size){
      ctx.patchState({error:'Choose a JPEG or PNG up to 2 MiB. The server also validates decoding and dimensions.'});
      return throwError(()=>new Error('Invalid image'));
    }
    return this.mutate(ctx,a.tour,()=>this.api.upload(a.tour,a.id,a.file));
  }
  @Action(RemoveImage) remove(ctx:StateContext<ImageStateModel>,a:RemoveImage){return this.mutate(ctx,a.tour,()=>this.api.delete(a.tour,a.id));}
  @Action(RecoverImage) recover(ctx:StateContext<ImageStateModel>,a:RecoverImage){return this.mutate(ctx,a.tour,()=>this.api.recover(a.tour,a.id));}
  private mutate(ctx:StateContext<ImageStateModel>,tour:string,call:()=>Observable<unknown>){
    if(ctx.getState().saving)return throwError(()=>new Error('An image change is already in progress.'));
    const g=this.generation,r=++this.request,current=()=>g===this.generation&&r===this.request&&ctx.getState().tour===tour;
    ctx.patchState({saving:true,loading:false,error:null});
    return call().pipe(switchMap(()=>this.api.list(tour)),tap(items=>{if(current())ctx.patchState({items});}),
      catchError((e:unknown)=>{if(current())ctx.patchState({error:message(e)});return throwError(()=>e);}),
      finalize(()=>{if(g===this.generation)ctx.patchState({saving:false});}));
  }
}
function message(e:unknown){
  if(e instanceof HttpErrorResponse){
    if(e.status===409)return 'The image changed, is leased, or the three-image limit was reached. Reload before retrying.';
    if(e.status===413||e.status===415||e.status===400)return 'Choose a valid JPEG or PNG, at most 2 MiB and 2048 × 2048 pixels.';
    if(e.status===403)return 'Administrator access is required.';
    if(e.status===401)return 'Your session expired. Sign in again.';
    if(e.status===404)return 'This tour or image is unavailable.';
  }
  return 'Image storage is unavailable. Reload to check the durable upload before retrying the same selected file.';
}
