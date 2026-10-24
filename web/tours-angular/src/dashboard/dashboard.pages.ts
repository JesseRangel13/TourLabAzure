import { Component, DestroyRef, inject } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Store } from '@ngxs/store';
import { AuthState } from '../auth/auth.state';
import { DashboardState, LoadDashboardPage, LoadDashboardBooking, RebuildDashboard } from './dashboard.state';
@Component({
  standalone:true,imports:[RouterLink,CurrencyPipe,DatePipe],
  template:`<main><h1>My booking dashboard</h1>
    <p>Cosmos projection · Eventually consistent. Status may lag. All payments and confirmations are simulated.</p>
    <a routerLink="/reservations">Authoritative bookings and actions</a>
    <button (click)="load()" [disabled]="vm().loading">First page / Refresh</button>
    @if(vm().loading){<p role="status">Loading dashboard…</p>}
    @if(vm().error){<p role="alert">{{vm().error}}</p><button (click)="load()">Retry dashboard</button>}
    @if(vm().page;as page){
      @if(!page.items.length){<p>No projected bookings on this page. Check the authoritative list for recent changes.</p>}
      <ul>@for(booking of page.items;track booking.id){<li>
        <a [routerLink]="['/dashboard',booking.reservationId]">{{booking.tourNameSnapshot}}</a>
        · {{booking.departureUtc|date:'medium':'UTC'}} UTC · {{booking.status}}
        · {{booking.amountCents/100|currency:'MXN'}} · {{booking.participants}} participants
        <small>Business version {{booking.businessVersion}}; projected {{booking.projectedAtUtc|date:'medium':'UTC'}} UTC</small>
        <a [routerLink]="['/reservations',booking.reservationId]">Authoritative details and actions</a>
      </li>}</ul>
      <button (click)="load(page.cursor)" [disabled]="!page.cursor||vm().loading">Next dashboard page</button>
    }
    @if(auth().user?.role==='Admin'){
      <section aria-label="Projection recovery"><h2>Projection recovery</h2>
        <p>Enqueue at most 25 owned SQL snapshots per batch. This does not delete data or wait for Cosmos.</p>
        <button (click)="rebuild(true)" [disabled]="vm().rebuilding">Start projection rebuild</button>
        <button (click)="rebuild(false)" [disabled]="!vm().rebuild?.cursor||vm().rebuilding">Continue rebuild batch</button>
        @if(vm().rebuilding){<p role="status">Enqueuing rebuild batch…</p>}
        @if(vm().rebuildError){<p role="alert">{{vm().rebuildError}}</p>}
        @if(vm().rebuild;as result){<p>{{result.enqueued}} snapshots enqueued. {{result.cursor?'More source rows remain.':'Source scan complete; projection may still lag.'}}</p>}
      </section>
    }
  </main>`
})
export class DashboardPageComponent {
  private readonly store=inject(Store);readonly vm=this.store.selectSignal(DashboardState.view);
  readonly auth=this.store.selectSignal(AuthState.view);
  constructor(){this.load();}
  load(cursor:string|null=null){this.store.dispatch(new LoadDashboardPage(cursor)).subscribe({error:()=>{}});}
  rebuild(restart:boolean){this.store.dispatch(new RebuildDashboard(restart)).subscribe({error:()=>{}});}
}
@Component({
  standalone:true,imports:[RouterLink,CurrencyPipe,DatePipe],
  template:`<main><h1>Projected booking</h1><p>Cosmos projection · Eventually consistent. Use authoritative details for cancellation and current status.</p>
    <a routerLink="/dashboard">Booking dashboard</a> <a routerLink="/reservations">Authoritative bookings</a>
    @if(vm().detailLoading){<p role="status">Loading projected booking…</p>}
    @if(vm().error){<p role="alert">{{vm().error}}</p><button (click)="reload()">Retry projected booking</button>}
    @if(vm().detail;as booking){
      <h2>{{booking.tourNameSnapshot}}</h2><p>{{booking.departureUtc|date:'medium':'UTC'}} UTC · {{booking.status}}</p>
      <p>{{booking.amountCents/100|currency:'MXN'}} · {{booking.participants}} participants</p>
      <p>Business version {{booking.businessVersion}}; projected {{booking.projectedAtUtc|date:'medium':'UTC'}} UTC</p>
      <a [routerLink]="['/reservations',booking.reservationId]">Authoritative details and actions</a>
    }
  </main>`
})
export class DashboardDetailComponent {
  private readonly store=inject(Store);readonly vm=this.store.selectSignal(DashboardState.view);private id='';
  constructor(){inject(ActivatedRoute).paramMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(params=>{this.id=params.get('id')??'';this.reload();});}
  reload(){this.store.dispatch(new LoadDashboardBooking(this.id)).subscribe({error:()=>{}});}
}
