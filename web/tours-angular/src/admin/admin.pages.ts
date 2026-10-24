import { Component, inject } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { JsonPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Store } from '@ngxs/store';
import { AdminState, ChangeFault, InspectDlq, LoadFault, LoadOperations, LoadSagas, RunAdminCommand } from './admin.state';
import { modes, Service, services } from './admin.api';
@Component({standalone:true,imports:[ReactiveFormsModule,RouterLink],template:`
 <main><h1>Saga diagnostics</h1><a routerLink="/simulations">Simulated provider and faults</a>
 <form [formGroup]="filters" (ngSubmit)="load(1)"><label>State filter <input formControlName="state"></label><button [disabled]="vm().loading">Filter sagas</button></form>
 @if(vm().error){<p role="alert">{{vm().error}}</p>} @if(vm().outcome){<p role="status">{{vm().outcome}}</p>}
 @if(vm().loading){<p role="status">Loading…</p>}
 @for(s of vm().sagas;track s.id){<section><h2>{{s.state}}</h2><a [routerLink]="['/reservations',s.reservationId]">{{s.reservationId}}</a>
 <p>Reason: {{s.reason}} · Payment requested: {{s.paymentRequested}} · resolved: {{s.paymentResolved}}</p>
 <p>Refund {{s.refundOperationId}} · required {{s.refundRequired}} · completed {{s.refundCompleted}}</p>
 <p>Seat release required {{s.seatsReleaseRequired}} · completed {{s.seatsReleased}}</p>
 <a [routerLink]="['/payments',s.paymentOperationId]">Simulated payment {{s.paymentOperationId}}</a>
 <button (click)="retry(s.id)" [disabled]="vm().mutating || (s.paymentRequested && !s.paymentResolved) || !['ManualReview','Compensating','CancellationPending'].includes(s.state)">Retry pending compensation</button></section>
 }@empty{<p>No sagas on this page.</p>}
 <p>Page {{page}}</p><button (click)="load(page-1)" [disabled]="page<=1 || vm().loading">Previous</button><button (click)="load(page+1)" [disabled]="vm().sagas.length<20 || vm().loading">Next</button>
 </main>`})
export class SagasPage {
 private readonly store=inject(Store);readonly vm=this.store.selectSignal(AdminState.view);readonly filters=inject(FormBuilder).nonNullable.group({state:['']});get filter(){return this.filters.controls.state.value;}page=1;
 constructor(){this.load();}
 load(page=1){this.page=page;this.store.dispatch(new LoadSagas(this.filter,page)).subscribe({error:()=>{}});}
 retry(id:string){this.store.dispatch(new RunAdminCommand({kind:'retry',id})).subscribe({error:()=>{}});}
}
@Component({standalone:true,imports:[ReactiveFormsModule],template:`
 <main><h1>Simulations</h1><p>Fictional providers only. Changing a mode does not alter an operation's captured mode.</p>
 <form [formGroup]="faultForm" (ngSubmit)="save()"><label>Service <select formControlName="service" (change)="changeService()">@for(s of services;track s){<option [value]="s">{{s}}</option>}</select></label>
 <label>Fault mode <select formControlName="mode">@for(m of choices();track m){<option [value]="m">{{m}}</option>}</select></label>
 <label>Occurrences <input formControlName="occurrences" type="number" min="0" max="100" step="1"></label>
 <button [disabled]="vm().mutating || faultForm.invalid">Apply simulated mode</button>
 <button type="button" (click)="reset()" [disabled]="vm().mutating">Reset modes</button></form>
 @if(vm().fault;as fault){<p>Current: {{fault.mode}} · remaining {{fault.remaining}}</p>}
 @if(vm().error){<p role="alert">{{vm().error}}</p>} @if(vm().outcome){<p role="status">{{vm().outcome}}</p>}
 <h2>Resolve an uncertain fictional provider</h2><p>Resolve only the provider result, then request reconciliation with the same operation ID.</p>
 <label>Payment operation ID <input [formControl]="operationControl"></label>
 <label>Definitive simulated outcome <select [formControl]="outcomeControl"><option>Succeeded</option><option>NotCharged</option></select></label>
 <button (click)="resolve()" [disabled]="vm().mutating || !validId()">Resolve fictional provider</button>
 <button (click)="reconcile()" [disabled]="vm().mutating || !validId()">Reconcile same payment</button>
 </main>`})
export class SimulationsPage {
 private readonly store=inject(Store);readonly vm=this.store.selectSignal(AdminState.view);readonly services=services;readonly faultForm=inject(FormBuilder).nonNullable.group({service:['catalog' as Service],mode:['None'],occurrences:[1,[Validators.required,Validators.min(0),Validators.max(100),(c:{value:number})=>Number.isInteger(c.value)?null:{integer:true}]]});
 readonly operationControl=new FormControl('',{nonNullable:true,validators:[Validators.pattern(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i),Validators.required]});
 readonly outcomeControl=new FormControl<'Succeeded'|'NotCharged'>('Succeeded',{nonNullable:true});
 get service(){return this.faultForm.controls.service.value;}get mode(){return this.faultForm.controls.mode.value;}get occurrences(){return this.faultForm.controls.occurrences.value;}
 get operation(){return this.operationControl.value;}get outcome(){return this.outcomeControl.value;}
 constructor(){this.changeService();}
 choices(){return modes[this.service];}integer(value:number){return Number.isInteger(value);}
 validId(){return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(this.operation);}
 changeService(){this.faultForm.controls.mode.setValue(modes[this.service][0]);this.store.dispatch(new LoadFault(this.service)).subscribe({error:()=>{}});}
 save(){if(this.faultForm.invalid)return;this.store.dispatch(new ChangeFault(this.service,this.mode,this.occurrences)).subscribe({error:()=>{}});}
 reset(){this.store.dispatch(new ChangeFault(this.service,modes[this.service][0],0,true)).subscribe({error:()=>{}});}
 resolve(){if(!this.validId())return;this.store.dispatch(new RunAdminCommand({kind:'resolve',id:this.operation,outcome:this.outcome})).subscribe({error:()=>{}});}
 reconcile(){if(!this.validId())return;this.store.dispatch(new RunAdminCommand({kind:'reconcile',id:this.operation})).subscribe({error:()=>{}});}
}
@Component({standalone:true,imports:[ReactiveFormsModule,JsonPipe],template:`
 <main><h1>Service operations</h1><label>Service <select [formControl]="serviceControl" (change)="load(1)">@for(s of services;track s){<option [value]="s">{{s}}</option>}</select></label>
 <button (click)="load(page)" [disabled]="vm().loading">Refresh diagnostics</button>
 @if(vm().error){<p role="alert">{{vm().error}}</p>} @if(vm().outcome){<p role="status">{{vm().outcome}}</p>}
 @if(vm().diagnostics;as d){<p>Pending Outbox: {{d.pendingOutboxCount}} · oldest {{d.oldestPendingOutboxAgeSeconds}} seconds · process duplicates {{d.ignoredDuplicates}}</p>
 <pre>{{d | json}}</pre>}
 <p>Page {{page}}</p><button (click)="load(page-1)" [disabled]="page<=1 || vm().loading">Previous</button><button (click)="load(page+1)" [disabled]="vm().loading || !hasNext()">Next</button>
 <h2>Built-in dead-letter queue</h2><p>Peek does not lock or remove messages. Replay locks at most 50 items, sends to the original queue or topic, then completes the original after acceptance. Topic replay may reach other matching subscriptions; SQL deduplication protects business effects.</p>
 <label>Source <select [formControl]="sourceControl" (change)="from=0"><option>commands</option><option>events</option></select></label>
 <button (click)="peek(true)" [disabled]="vm().brokerLoading">Peek first page</button>
 <button (click)="peek(false)" [disabled]="vm().brokerLoading || vm().deadLetters.length<10">Peek next page</button>
 @if(vm().brokerLoading){<p role="status">Inspecting…</p>}@if(vm().brokerError){<p role="alert">{{vm().brokerError}}</p>}
 @for(item of vm().deadLetters;track item.id){<section><p>{{item.source}} #{{item.sequenceNumber}} · {{item.type}} · {{item.reason}}</p>
 <p>Logical message {{item.logicalMessageId}}</p><button (click)="replay(item.id)" [disabled]="vm().mutating || !item.replayable">Replay inspected item</button></section>
 }@empty{<p>No inspected dead letters.</p>}
 </main>`})
export class OperationsPage {
 private readonly store=inject(Store);readonly vm=this.store.selectSignal(AdminState.view);readonly services=services;readonly serviceControl=new FormControl<Service>('catalog',{nonNullable:true});readonly sourceControl=new FormControl<'commands'|'events'>('commands',{nonNullable:true});page=1;from=0;
 get service(){return this.serviceControl.value;}get source(){return this.sourceControl.value;}
 constructor(){this.load();}
 load(page=1){this.page=page;this.from=0;this.store.dispatch(new LoadOperations(this.service,page)).subscribe({error:()=>{}});}
 hasNext(){const d=this.vm().diagnostics;return !!d&&(d.outbox.length===10||d.inbox.length===10||d.audit.length===10);}
 peek(first:boolean){this.from=first?0:Math.max(...this.vm().deadLetters.map(x=>x.sequenceNumber))+1;this.store.dispatch(new InspectDlq(this.service,this.source,this.from)).subscribe({error:()=>{}});}
 replay(id:string){this.store.dispatch(new RunAdminCommand({kind:'replay',id,service:this.service})).subscribe({error:()=>{}});}
}
