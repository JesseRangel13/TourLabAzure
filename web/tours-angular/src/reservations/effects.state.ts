import { Injectable, inject, Component, DestroyRef } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { State, Action, Selector, StateContext, Store } from '@ngxs/store';
import { timeout, tap, catchError, finalize, throwError } from 'rxjs';
import { UserSessionChanged } from '../auth/auth.state';
import { Payment, Notification } from './reservations.api';
export interface PaymentDetail extends Payment { mode:string;reason:string|null;reconcileAttempts:number;refund:{id:string;status:string;attempts:number;reason:string|null}|null; }
export interface Projection {sessionId:string;name:string;active:boolean;priceVersion:number;unitAmountMinor:number;updatedAtUtc:string;}
@Injectable({providedIn:'root'})
export class EffectsApi {
 private readonly http=inject(HttpClient);
 detail(kind:'payments'|'notifications',id:string){return this.http.get<PaymentDetail|Notification>('/api/'+kind+'/v1/'+kind+'/'+id).pipe(timeout(10_000));}
 projections(page:number){return this.http.get<Projection[]>('/api/reservations/v1/projections/tour-sessions',{params:{page,pageSize:10}}).pipe(timeout(10_000));}
}
export class LoadEffectDetail {static readonly type='[Effects] Detail';constructor(readonly kind:'payments'|'notifications',readonly id:string){}}
export class LoadProjections {static readonly type='[Effects] Projection';constructor(readonly page=1){}}
interface Model {payment:PaymentDetail|null;notification:Notification|null;projections:Projection[];loading:boolean;error:string|null;}
const initial=():Model=>({payment:null,notification:null,projections:[],loading:false,error:null});
@State<Model>({name:'effects',defaults:initial()})
@Injectable()
export class EffectsState {
 private readonly api=inject(EffectsApi);private generation=0;private query=0;
 @Selector() static view(state:Model){return state;}
 @Action(UserSessionChanged) reset(ctx:StateContext<Model>){++this.generation;++this.query;ctx.setState(initial());}
 @Action(LoadEffectDetail,{cancelUncompleted:true})
 detail(ctx:StateContext<Model>,a:LoadEffectDetail){
  const generation=this.generation,query=++this.query;const current=()=>generation===this.generation&&query===this.query;
  ctx.patchState({payment:null,notification:null,loading:true,error:null});
  return this.api.detail(a.kind,a.id).pipe(
   tap(value=>{if(current())ctx.patchState(a.kind==='payments'?{payment:value as PaymentDetail}:{notification:value as Notification});}),
   catchError(e=>{if(current())ctx.patchState({error:e instanceof HttpErrorResponse&&e.status===404?'This effect is unavailable.':'Effect data is unavailable. Please retry.'});return throwError(()=>e);}),
   finalize(()=>{if(current())ctx.patchState({loading:false});}));
 }
 @Action(LoadProjections,{cancelUncompleted:true})
 projections(ctx:StateContext<Model>,a:LoadProjections){
  const generation=this.generation,query=++this.query;const current=()=>generation===this.generation&&query===this.query;
  ctx.patchState({projections:[],loading:true,error:null});
  return this.api.projections(a.page).pipe(
   tap(projections=>{if(current())ctx.patchState({projections});}),catchError(e=>{if(current())ctx.patchState({error:'Projection unavailable. Use Catalog for authoritative availability.'});return throwError(()=>e);}),
   finalize(()=>{if(current())ctx.patchState({loading:false});}));
 }
}
@Component({standalone:true,imports:[CurrencyPipe,RouterLink],template:`
 <main><h1>Simulated effect details</h1><button (click)="load()" [disabled]="vm().loading">Refresh effect</button>
 @if(vm().error){<p role="alert">{{vm().error}}</p>}
 @if(vm().payment;as p){<h2>Simulated payment {{p.status}}</h2><p>{{p.id}} · {{p.amountMinor/100|currency:'MXN'}} · mode {{p.mode}}</p><p>{{p.reason}} · reconciliation attempts {{p.reconcileAttempts}}</p><a [routerLink]="['/reservations',p.reservationId]">Booking details</a>
 @if(p.refund;as r){<h2>Simulated refund {{r.status}}</h2><p>{{r.id}} · attempts {{r.attempts}} · {{r.reason}}</p>}}
 @if(vm().notification;as n){<h2>{{n.subject}}</h2><p>{{n.status}} · SIMULATED DELIVERY</p><p>{{n.body}}</p><a [routerLink]="['/reservations',n.reservationId]">Booking details</a>}
 </main>`})
export class EffectDetailPage {
 private readonly store=inject(Store);private readonly route=inject(ActivatedRoute);private readonly destroy=inject(DestroyRef);
 readonly vm=this.store.selectSignal(EffectsState.view);private id='';private readonly kind=this.route.snapshot.data['kind'] as 'payments'|'notifications';
 constructor(){this.route.paramMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(p=>{this.id=p.get('id')??'';this.load();});}
 load(){this.store.dispatch(new LoadEffectDetail(this.kind,this.id)).subscribe({error:()=>{}});}
}
@Component({standalone:true,imports:[CurrencyPipe,DatePipe,RouterLink],template:`
 <main><h1>Informational tour prices</h1><p>Eventual projection; it can be stale. <a routerLink="/tours">Use Catalog for authoritative price and availability.</a></p>
 <button (click)="load(page)" [disabled]="vm().loading">Refresh projection</button>@if(vm().error){<p role="alert">{{vm().error}}</p>}
 @for(p of vm().projections;track p.sessionId){<section><h2>{{p.name}}</h2><p>{{p.sessionId}} · active {{p.active}} · {{p.unitAmountMinor/100|currency:'MXN'}} · price version {{p.priceVersion}}</p><p>Updated {{p.updatedAtUtc|date:'medium':'UTC'}} UTC</p></section>}@empty{<p>No projections on this page.</p>}
 <p>Page {{page}}</p><button (click)="load(page-1)" [disabled]="page<=1 || vm().loading">Previous</button><button (click)="load(page+1)" [disabled]="vm().projections.length<10 || vm().loading">Next</button></main>`})
export class ProjectionsPage {
 private readonly store=inject(Store);readonly vm=this.store.selectSignal(EffectsState.view);page=1;
 constructor(){this.load();}load(page=1){this.page=page;this.store.dispatch(new LoadProjections(page)).subscribe({error:()=>{}});}
}
