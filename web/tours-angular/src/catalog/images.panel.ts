import { Component, effect, inject, input, signal } from '@angular/core';
import { Store } from '@ngxs/store';
import { AuthState } from '../auth/auth.state';
import { ImagesState, LoadImages, UploadImage, RemoveImage, RecoverImage } from './images.state';
@Component({
  selector:'app-tour-images',standalone:true,
  template:`<section aria-label="Tour images"><h2>Tour images</h2>
    @if(vm().loading){<p role="status">Loading images…</p>}
    @if(vm().error){<p role="alert">{{vm().error}}</p>}
    <button (click)="reload()" [disabled]="vm().loading||vm().saving">Reload images</button>
    @if(!vm().items.length&&!vm().loading){<p>No ready tour images. Image placeholder.</p>}
    @for(image of vm().items;track image.id){
      <figure>
        @if(image.status==='Ready'&&image.url&&!failed()[image.id]){
          <img [src]="image.url" alt="Tour photograph" width="240" height="180" (error)="broken(image.id)">
        }@else{<p>Image placeholder · {{image.status==='Ready'?'Image unavailable':image.status}}</p>}
        <figcaption>{{image.status}} · {{image.byteSize}} bytes · {{image.attempts}} attempts</figcaption>
        @if(auth().user?.role==='Admin'){
          @if(image.status!=='Deleted'){<button (click)="remove(image.id)" [disabled]="vm().saving">Remove image</button>}
          @if(image.status!=='Ready'){<button (click)="recover(image.id)" [disabled]="vm().saving">Recover image</button>}
        }
      </figure>
    }
    @if(auth().user?.role==='Admin'){
      <label>JPEG or PNG image <input type="file" accept="image/jpeg,image/png" (change)="select($event)" [disabled]="vm().saving"></label>
      <p>Maximum three images per tour; 2 MiB and 2048 × 2048 pixels each. Deletion and recovery are durable background work.</p>
      <button (click)="upload()" [disabled]="!selected||vm().saving">Upload image</button>
    }
  </section>`
})
export class TourImagesPanel {
  readonly tour=input.required<string>();private readonly store=inject(Store);
  readonly vm=this.store.selectSignal(ImagesState.view);readonly auth=this.store.selectSignal(AuthState.view);
  readonly failed=signal<Record<string,boolean>>({});selected:File|null=null;private id='';
  constructor(){effect(()=>{this.tour();this.auth().user?.userId;this.selected=null;this.id='';this.reload();});}
  reload(){this.failed.set({});this.store.dispatch(new LoadImages(this.tour())).subscribe({error:()=>{}});}
  select(event:Event){this.selected=(event.target as HTMLInputElement).files?.[0]??null;this.id=crypto.randomUUID();}
  upload(){if(this.selected)this.store.dispatch(new UploadImage(this.tour(),this.id,this.selected)).subscribe({next:()=>{this.selected=null;},error:()=>{}});}
  remove(id:string){this.store.dispatch(new RemoveImage(this.tour(),id)).subscribe({error:()=>{}});}
  recover(id:string){this.store.dispatch(new RecoverImage(this.tour(),id)).subscribe({error:()=>{}});}
  broken(id:string){this.failed.update(value=>({...value,[id]:true}));}
}
