import { Component, DestroyRef, inject, input } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Store } from '@ngxs/store';
import { Session } from '../catalog/catalog.api';
import { LoadEffects, LoadReservation, LoadReservations, ReservationsState, SubmitBooking } from './reservations.state';
@Component({
  selector: 'app-booking-form', standalone: true, imports: [ReactiveFormsModule, CurrencyPipe],
  template: `<form [formGroup]="form" (ngSubmit)="submit()">
    <label>Participants <input type="number" formControlName="participants" min="1" max="6" step="1"></label>
    <p>Accepted price: {{ session().unitAmountMinor / 100 | currency:'MXN' }} per participant.</p>
    <button [disabled]="form.invalid || vm().creating || session().availableSeats === 0">Book session</button>
    @if (vm().creating) { <p role="status">Submitting booking…</p> }
    @if (vm().error) { <p role="alert">{{ vm().error }}</p> }
    <small>The same unchanged request keeps its retry key. Payments and notification delivery are simulated.</small>
  </form>`
})
export class BookingForm {
  readonly session = input.required<Session>();
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  readonly vm = this.store.selectSignal(ReservationsState.view);
  readonly form = inject(FormBuilder).nonNullable.group({ participants: [1, [Validators.required, Validators.min(1), Validators.max(6),
    (control: { value: number }) => Number.isInteger(control.value) ? null : { integer: true }]] });
  private fingerprint = '';
  private key = '';
  submit() {
    if (this.form.invalid || this.vm().creating) return;
    const session = this.session();
    const input = { sessionId: session.id, participants: this.form.controls.participants.value,
      expectedUnitAmountMinor: session.unitAmountMinor, expectedCurrency: session.currency };
    const fingerprint = JSON.stringify(input);
    if (fingerprint !== this.fingerprint) { this.fingerprint = fingerprint; this.key = crypto.randomUUID(); }
    const key = this.key;
    this.store.dispatch(new SubmitBooking(input, key)).subscribe({ next: () => {
      const state = this.store.selectSnapshot(ReservationsState.view);
      if (state.accepted && state.acceptedKey === key) void this.router.navigate(['/reservations', state.accepted.reservationId]);
    }, error: () => {} });
  }
}
@Component({
  standalone: true, imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `<main><h1>My bookings</h1><button (click)="load()" [disabled]="vm().loading">Refresh bookings</button>
    @if (vm().loading) { <p role="status">Loading bookings…</p> }
    @if (vm().error) { <p role="alert">{{ vm().error }}</p> }
    @if (vm().page; as page) {
      @if (!page.items.length) { <p>No bookings yet. <a routerLink="/tours">Browse tours</a></p> }
      @for (booking of page.items; track booking.id) { <section>
        <a [routerLink]="['/reservations', booking.id]">{{ booking.id }}</a>
        <p>{{ booking.status }} · {{ booking.startsAtUtc | date:'medium':'UTC' }} UTC · {{ booking.totalAmountMinor / 100 | currency:'MXN' }}</p>
      </section> }
      <p>Page {{ page.page }} · {{ page.total }} bookings</p>
      <button (click)="load(page.page - 1)" [disabled]="page.page <= 1 || vm().loading">Previous</button>
      <button (click)="load(page.page + 1)" [disabled]="page.page * page.pageSize >= page.total || vm().loading">Next</button>
    }
  </main>`
})
export class ReservationsPage {
  private readonly store = inject(Store);
  readonly vm = this.store.selectSignal(ReservationsState.view);
  constructor() { this.load(); }
  load(page = 1) { this.store.dispatch(new LoadReservations(page)).subscribe({ error: () => {} }); }
}
@Component({
  standalone: true, imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `<main><a routerLink="/reservations">My bookings</a><h1>Booking details</h1>
    <button (click)="load()" [disabled]="vm().loading">Refresh booking</button>
    @if (vm().loading) { <p role="status">Loading booking…</p> }
    @if (vm().error) { <p role="alert">{{ vm().error }}</p> }
    @if (vm().detail; as booking) {
      <p>{{ booking.id }}</p><h2>{{ booking.status }}</h2>
      <p>{{ booking.participants }} participants · {{ booking.startsAtUtc | date:'medium':'UTC' }} UTC</p>
      <p>Accepted total: {{ booking.totalAmountMinor / 100 | currency:'MXN' }}</p>
      @if (booking.reason) { <p>{{ booking.reason }}</p> }
      <h2>Authoritative timeline</h2><ol>@for (entry of vm().timeline; track $index) {
        <li>{{ entry.from }} → {{ entry.to }} · {{ entry.occurredAtUtc | date:'medium':'UTC' }} UTC @if (entry.reason) { · {{ entry.reason }} }</li>
      }</ol>
      <p>Refresh to see the latest booking status.</p>
    }
    <h2>Simulated payments</h2>
    @if (vm().effectsLoading) { <p role="status">Loading simulated effects…</p> }
    @if (vm().effectsError) { <p role="alert">{{ vm().effectsError }}</p><button (click)="effects()">Retry simulated effects</button> }
    @for (payment of vm().payments; track payment.id) { <p>{{ payment.id }} · {{ payment.status }} · {{ payment.amountMinor / 100 | currency:'MXN' }} · SIMULATED</p> }
    <h2>Simulated notifications</h2>
    @for (notification of vm().notifications; track notification.id) { <section><h3>{{ notification.subject }}</h3><p>{{ notification.status }} · SIMULATED DELIVERY</p><p>{{ notification.body }}</p></section> }
  </main>`
})
export class ReservationDetailPage {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private id = '';
  readonly vm = this.store.selectSignal(ReservationsState.view);
  constructor() { this.route.paramMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(params => { this.id = params.get('id') ?? ''; this.load(); }); }
  load() { this.store.dispatch(new LoadReservation(this.id)).subscribe({ error: () => {} }); this.effects(); }
  effects() { this.store.dispatch(new LoadEffects(this.id)).subscribe({ error: () => {} }); }
}
@Component({
  standalone: true, imports: [CurrencyPipe, RouterLink],
  template: `<main><h1>{{ kind === 'payments' ? 'My simulated payments' : 'My simulated notifications' }}</h1>
    <button (click)="load()" [disabled]="vm().effectsLoading">Refresh</button>
    @if (vm().effectsError) { <p role="alert">{{ vm().effectsError }}</p> }
    @if (vm().effectsLoading) { <p role="status">Loading…</p> }
    @if (kind === 'payments') { @for (payment of vm().payments; track payment.id) {
      <section><a [routerLink]="['/reservations', payment.reservationId]">{{ payment.id }}</a><p>{{ payment.status }} · {{ payment.amountMinor / 100 | currency:'MXN' }} · SIMULATED</p></section>
    } @empty { <p>No payments on this page.</p> } }
    @else { @for (notification of vm().notifications; track notification.id) {
      <section><a [routerLink]="['/reservations', notification.reservationId]">{{ notification.subject }}</a><p>{{ notification.status }} · SIMULATED DELIVERY</p><p>{{ notification.body }}</p></section>
    } @empty { <p>No notifications on this page.</p> } }
        <p>Page {{ vm().effectsPage }} · showing up to 10 records</p>
    <button (click)="load(vm().effectsPage - 1)" [disabled]="vm().effectsPage <= 1 || vm().effectsLoading">Previous</button>
    <button (click)="load(vm().effectsPage + 1)" [disabled]="vm().effectsLoading || (kind === 'payments' ? vm().payments.length < 10 : vm().effectsPage * 10 >= vm().notificationTotal)">Next</button>
  </main>`
})
export class PersonalEffectsPage {
  private readonly store = inject(Store);
  readonly kind = inject(ActivatedRoute).snapshot.data['kind'] as 'payments' | 'notifications';
  readonly vm = this.store.selectSignal(ReservationsState.view);
  constructor() { this.load(); }
  load(page = 1) { this.store.dispatch(new LoadEffects(undefined, page)).subscribe({ error: () => {} }); }
}