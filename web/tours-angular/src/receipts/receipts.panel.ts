import { Component, inject, input, effect } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Store } from '@ngxs/store';
import { DownloadReceipt, LoadReceipts, ReceiptsState } from './receipts.state';
@Component({
  selector:'app-receipts',standalone:true,imports:[DatePipe],
  template:`<section aria-label="Historical simulated confirmations"><h2>Simulated booking confirmation</h2>
    <p>Historical educational document. It is not a legal tax invoice or an actual payment receipt.
    Current authoritative booking status: <strong>{{ currentStatus() }}</strong>.
    A later cancellation does not change an earlier confirmation.</p>
    <button (click)="load()" [disabled]="vm().loading">Refresh confirmation</button>
    @if(vm().loading){<p role="status">Loading confirmation status…</p>}
    @if(vm().error){<p role="alert">{{ vm().error }}</p>}
    @for(receipt of vm().items;track receipt.id){
      <p>{{ receipt.status }} · Confirmed {{ receipt.confirmedAtUtc | date:'medium':'UTC' }} UTC · SIMULATED</p>
      @if(receipt.status==='Ready'){
        <button (click)="download(receipt.id)" [disabled]="vm().downloading">Download simulated confirmation</button>
      } @else if(receipt.status==='Conflict'){
        <p role="alert">Document integrity conflict. The existing document was preserved. Contact the lab administrator.</p>
      } @else {<p role="status">Confirmation generation is pending. Refresh later; booking does not wait for the Function.</p>}
    } @empty {
      @if(!vm().loading&&!vm().error){<p>No confirmation is available yet. Only confirmed bookings create new documents.</p>}
    }
  </section>`
})
export class ReceiptsPanel {
  readonly reservation=input.required<string>();readonly currentStatus=input.required<string>();
  private readonly store=inject(Store);readonly vm=this.store.selectSignal(ReceiptsState.view);
  constructor(){effect(()=>{this.store.dispatch(new LoadReceipts(this.reservation())).subscribe({error:()=>{}});});}
  load(){this.store.dispatch(new LoadReceipts(this.reservation())).subscribe({error:()=>{}});}
  download(id:string){this.store.dispatch(new DownloadReceipt(id)).subscribe({error:()=>{}});}
}