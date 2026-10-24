import { Component, DestroyRef, effect, inject, input } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Store } from '@ngxs/store';
import { AuthState } from '../auth/auth.state';
import { Session } from './catalog.api';
import { CatalogState, ChangeSessionCapacity, ChangeSessionPrice, CreateCatalogSession, LoadCatalogTour, LoadTours, SaveTour } from './catalog.state';
const integer = (control: { value: number }) => Number.isSafeInteger(control.value) ? null : { integer: true };

@Component({
  standalone: true, selector: 'app-session-editor', imports: [ReactiveFormsModule],
  template: `<form [formGroup]="form" (ngSubmit)="price()">
    <label>Price in MXN cents <input type="number" formControlName="amount" min="1" step="1"></label>
    <button [disabled]="!form.controls.amount.valid || vm().saving">Update price</button>
    <label>Capacity <input type="number" formControlName="capacity" min="0" step="1"></label>
    <button type="button" (click)="capacity()" [disabled]="!form.controls.capacity.valid || vm().saving">Update capacity</button>
    <small>Version {{ session().version }}. Reload after a version conflict.</small>
  </form>`
})
export class SessionEditor {
  readonly session = input.required<Session>();
  private readonly store = inject(Store);
  readonly vm = this.store.selectSignal(CatalogState.view);
  readonly form = inject(FormBuilder).nonNullable.group({
    amount: [1, [Validators.required, Validators.min(1), integer]],
    capacity: [0, [Validators.required, Validators.min(0), Validators.max(2147483647), integer]]
  });
  constructor() { effect(() => this.form.setValue({ amount: this.session().unitAmountMinor, capacity: this.session().capacity })); }
  price() {
    if (!this.form.controls.amount.valid) return;
    this.store.dispatch(new ChangeSessionPrice(this.session(), this.form.controls.amount.value)).subscribe({ error: () => {} });
  }
  capacity() {
    if (!this.form.controls.capacity.valid) return;
    this.store.dispatch(new ChangeSessionCapacity(this.session(), this.form.controls.capacity.value)).subscribe({ error: () => {} });
  }
}
@Component({
  standalone: true, imports: [ReactiveFormsModule, RouterLink],
  template: `<main><p>CATALOG · AZ-003</p><h1>Tours</h1>
    <form [formGroup]="search" (ngSubmit)="load()">
      <label>Search tours <input formControlName="text" maxlength="200"></label><button [disabled]="vm().listLoading">Search</button>
    </form>
    @if (vm().listLoading) { <p role="status">Loading tours…</p> }
    @if (vm().error) { <p role="alert">{{ vm().error }}</p><button (click)="load()">Retry</button> }
    @if (vm().page; as page) {
      @if (!page.items.length) { <p>No tours match your search.</p> }
      <ul>@for (tour of page.items; track tour.id) {
        <li><a [routerLink]="['/tours', tour.id]">{{ tour.name }}</a> @if (!tour.active) { <span>Inactive</span> }</li>
      }</ul>
      <p>Page {{ page.page }} · {{ page.total }} tours</p>
      <button (click)="load(page.page - 1)" [disabled]="page.page <= 1 || vm().listLoading">Previous</button>
      <button (click)="load(page.page + 1)" [disabled]="page.page * page.pageSize >= page.total || vm().listLoading">Next</button>
    }
    @if (auth().user?.role === 'Admin') {
      <section><h2>Create tour</h2><form [formGroup]="create" (ngSubmit)="save()">
        <label>Tour name <input formControlName="name" maxlength="200"></label>
        <label>Description <textarea formControlName="description" maxlength="4000"></textarea></label>
        <button [disabled]="create.invalid || vm().saving">Create tour</button>
      </form></section>
    }
    <p>Booking and images arrive in later tasks. Payments and notification deliveries are simulated.</p>
  </main>`
})
export class CatalogListPage {
  private readonly store = inject(Store);
  readonly vm = this.store.selectSignal(CatalogState.view);
  readonly auth = this.store.selectSignal(AuthState.view);
  private readonly fb = inject(FormBuilder);
  readonly search = this.fb.nonNullable.group({ text: ['', Validators.maxLength(200)] });
  readonly create = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(/.*\S.*/)]],
    description: ['', Validators.maxLength(4000)]
  });
  constructor() { this.load(); }
  load(page = 1) { this.store.dispatch(new LoadTours(this.search.controls.text.value, page)).subscribe({ error: () => {} }); }
  save() {
    if (this.create.invalid) return;
    this.store.dispatch(new SaveTour(this.create.getRawValue())).subscribe({ next: () => { this.create.reset(); this.load(); }, error: () => {} });
  }
}
@Component({
  standalone: true, imports: [ReactiveFormsModule, RouterLink, DatePipe, CurrencyPipe, SessionEditor],
  template: `<main><a routerLink="/tours">All tours</a>
    @if (vm().detailLoading) { <p role="status">Loading tour…</p> }
    @if (vm().error) { <p role="alert">{{ vm().error }}</p> }
    <button (click)="reload()" [disabled]="vm().saving || vm().detailLoading">Reload tour</button>
    @if (vm().tour; as tour) {
      <h1>{{ tour.name }}</h1><p>{{ tour.description }}</p><p>{{ tour.active ? 'Active' : 'Inactive' }}</p>
      @if (auth().user?.role === 'Admin') {
        <h2>Edit tour</h2><form [formGroup]="edit" (ngSubmit)="save()">
          <label>Tour name <input formControlName="name" maxlength="200"></label>
          <label>Description <textarea formControlName="description" maxlength="4000"></textarea></label>
          <label><input type="checkbox" formControlName="active"> Active tour</label>
          <button [disabled]="edit.invalid || vm().saving || vm().detailLoading">Save tour</button>
        </form>
        <h2>Create session</h2><form [formGroup]="create" (ngSubmit)="addSession()">
          <label>Departure in UTC <input type="datetime-local" formControlName="departure"></label>
          <label>Price in MXN cents <input type="number" formControlName="amount" min="1" step="1"></label>
          <label>Capacity <input type="number" formControlName="capacity" min="0" step="1"></label>
          <button [disabled]="create.invalid || vm().saving || vm().detailLoading">Create session</button>
          @if (create.controls.departure.hasError('future')) { <p role="alert">Choose a future UTC departure.</p> }
        </form>
      }
      <h2>Sessions</h2>
      @if (!vm().sessions.length) { <p>No sessions available.</p> }
      @for (session of vm().sessions; track session.id) {
        <section [attr.aria-label]="'Session ' + session.startsAtUtc">
          <h3>{{ session.startsAtUtc | date:'medium':'UTC' }} UTC</h3>
          <p>{{ session.unitAmountMinor / 100 | currency:'MXN' }} per participant · {{ session.availableSeats }} / {{ session.capacity }} seats available</p>
          @if (auth().user?.role === 'Admin') { <app-session-editor [session]="session" /> }
        </section>
      }
    }
  </main>`
})
export class CatalogDetailPage {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private id = '';
  readonly vm = this.store.selectSignal(CatalogState.view);
  readonly auth = this.store.selectSignal(AuthState.view);
  private readonly fb = inject(FormBuilder);
  readonly edit = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(/.*\S.*/)]],
    description: ['', Validators.maxLength(4000)], active: [true]
  });
  readonly create = this.fb.nonNullable.group({
    departure: ['', Validators.required], amount: [10000, [Validators.required, Validators.min(1), integer]],
    capacity: [1, [Validators.required, Validators.min(0), Validators.max(2147483647), integer]]
  });
  constructor() {
    const currentTour = this.store.selectSignal(CatalogState.tour);
    effect(() => { const tour = currentTour(); if (tour) this.edit.setValue({ name: tour.name, description: tour.description, active: tour.active }); });
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(params => { this.id = params.get('id') ?? ''; this.reload(); });
  }
  reload() { this.store.dispatch(new LoadCatalogTour(this.id)).subscribe({ error: () => {} }); }
  save() {
    const tour = this.vm().tour;
    if (!tour || this.edit.invalid) return;
    const value = this.edit.getRawValue();
    this.store.dispatch(new SaveTour(value, tour, value.active)).subscribe({ next: () => this.reload(), error: () => {} });
  }
  addSession() {
    if (this.create.invalid) return;
    const value = this.create.getRawValue(), departure = new Date(value.departure + 'Z');
    if (!Number.isFinite(departure.valueOf()) || departure.valueOf() <= Date.now()) {
      this.create.controls.departure.setErrors({ future: true }); return;
    }
    this.store.dispatch(new CreateCatalogSession(this.id, { startsAtUtc: departure.toISOString(), unitAmountMinor: value.amount, capacity: value.capacity }))
      .subscribe({ next: () => this.create.reset(), error: () => {} });
  }
}