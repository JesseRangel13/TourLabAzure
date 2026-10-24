import { inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Action, Selector, State, StateContext } from '@ngxs/store';
import { catchError, finalize, forkJoin, of, tap, throwError } from 'rxjs';
import { UserSessionChanged } from '../auth/auth.state';
import { Accepted, BookingInput, Notification, Payment, Reservation, ReservationPage, ReservationsApi, Transition } from './reservations.api';
export class SubmitBooking {
  static readonly type = '[Reservations] Submit';
  constructor(readonly input: BookingInput, readonly key: string) {}
}
export class LoadReservations { static readonly type = '[Reservations] Load list'; constructor(readonly page = 1, readonly status="", readonly userId="") {} }
export class LoadReservation { static readonly type = '[Reservations] Load detail'; constructor(readonly id: string) {} }
export class CancelReservation { static readonly type="[Reservations] Cancel"; constructor(readonly id:string,readonly key:string){} }
export class LoadEffects { static readonly type = '[Reservations] Load simulated effects'; constructor(readonly id?: string, readonly page = 1, readonly kind: "both" | "payments" | "notifications" = "both") {} }
export interface ReservationsModel {
  page: ReservationPage | null; detail: Reservation | null; timeline: Transition[];
  payments: Payment[]; notifications: Notification[]; accepted: Accepted | null; acceptedKey: string | null;
  loading: boolean; creating: boolean; effectsLoading: boolean; error: string | null; effectsError: string | null; effectsPage: number; notificationTotal: number;
}
const defaults = (): ReservationsModel => ({
  page: null, detail: null, timeline: [], payments: [], notifications: [], accepted: null, acceptedKey: null,
  loading: false, creating: false, effectsLoading: false, error: null, effectsError: null, effectsPage: 1, notificationTotal: 0
});
@State<ReservationsModel>({ name: 'reservations', defaults: defaults() })
@Injectable()
export class ReservationsState {
  private readonly api = inject(ReservationsApi);
  private generation = 0;
  private query = 0;
  private effectsQuery = 0;
  @Selector() static view(state: ReservationsModel) { return state; }
  @Selector() static bookings(state: ReservationsModel) { return state.page?.items ?? []; }
  @Action(UserSessionChanged)
  reset(ctx: StateContext<ReservationsModel>) { ++this.generation; ++this.query; ++this.effectsQuery; ctx.setState(defaults()); }
  @Action(SubmitBooking)
  submit(ctx: StateContext<ReservationsModel>, action: SubmitBooking) {
    if (ctx.getState().creating) return throwError(() => new Error('A booking submission is already pending.'));
    const generation = this.generation;
    ctx.patchState({ creating: true, accepted: null, acceptedKey: null, error: null });
    return this.api.create(action.input, action.key).pipe(
      tap(accepted => { if (generation === this.generation) ctx.patchState({ accepted, acceptedKey: action.key, page: null }); }),
      catchError((error: unknown) => { if (generation === this.generation) ctx.patchState({ error: classify(error, true) }); return throwError(() => error); }),
      finalize(() => { if (generation === this.generation) ctx.patchState({ creating: false }); })
    );
  }
  @Action(CancelReservation)
  cancel(ctx:StateContext<ReservationsModel>,action:CancelReservation){
    if(ctx.getState().creating)return throwError(()=>new Error('A booking mutation is pending.'));
    const generation=this.generation;ctx.patchState({creating:true,error:null});
    return this.api.cancel(action.id,action.key).pipe(
      tap(()=>{if(generation===this.generation)ctx.patchState({page:null});}),
      catchError(error=>{if(generation===this.generation)ctx.patchState({error:classifyCancellation(error)});return throwError(()=>error);}),
      finalize(()=>{if(generation===this.generation)ctx.patchState({creating:false});}));
  }
  @Action(LoadReservations, { cancelUncompleted: true })
  list(ctx: StateContext<ReservationsModel>, action: LoadReservations) {
    const generation = this.generation, query = ++this.query;
    const current = () => generation === this.generation && query === this.query;
    ctx.patchState({ loading: true, page: null, error: null });
    return this.api.list(action.page, action.status, action.userId).pipe(
      tap(page => { if (current()) ctx.patchState({ page }); }),
      catchError((error: unknown) => { if (current()) ctx.patchState({ error: classify(error) }); return throwError(() => error); }),
      finalize(() => { if (current()) ctx.patchState({ loading: false }); })
    );
  }
  @Action(LoadReservation, { cancelUncompleted: true })
  detail(ctx: StateContext<ReservationsModel>, action: LoadReservation) {
    const generation = this.generation, query = ++this.query;
    const current = () => generation === this.generation && query === this.query;
    ctx.patchState({ loading: true, detail: null, timeline: [], error: null });
    return forkJoin({ detail: this.api.detail(action.id), timeline: this.api.timeline(action.id) }).pipe(
      tap(value => { if (current()) ctx.patchState(value); }),
      catchError((error: unknown) => { if (current()) ctx.patchState({ error: classify(error) }); return throwError(() => error); }),
      finalize(() => { if (current()) ctx.patchState({ loading: false }); })
    );
  }
  @Action(LoadEffects, { cancelUncompleted: true })
  effects(ctx: StateContext<ReservationsModel>, action: LoadEffects) {
    const generation = this.generation, query = ++this.effectsQuery;
    const current = () => generation === this.generation && query === this.effectsQuery;
    ctx.patchState({ effectsLoading: true, effectsError: null, payments: [], notifications: [] });
    return forkJoin({ payments: action.kind==="notifications"?of([]):this.api.payments(action.id, action.page), notifications: action.kind==="payments"?of({items:[],total:0,page:action.page,pageSize:10}):this.api.notifications(action.id, action.page) }).pipe(
      tap(result => { if (current()) ctx.patchState({ payments: result.payments, notifications: result.notifications.items, effectsPage: action.page, notificationTotal: result.notifications.total }); }),
      catchError((error: unknown) => { if (current()) ctx.patchState({ effectsError: classify(error) }); return throwError(() => error); }),
      finalize(() => { if (current()) ctx.patchState({ effectsLoading: false }); })
    );
  }
}
function classify(error: unknown, submitting = false) {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 409) return 'The price, availability or request changed. Reload and review before starting a new booking intention.';
    if (error.status === 404) return 'This reservation is unavailable.';
    if (error.status === 401) return 'Your session expired. Sign in again.';
    if (error.status === 400) return 'Check participants and the accepted price.';
  }
  return submitting ? 'The booking result is uncertain. Retry unchanged values or check My bookings before starting a new intention.' :
    'Booking data is unavailable. Please retry.';
}
function classifyCancellation(error:unknown){
  if(error instanceof HttpErrorResponse){
    if(error.status===409)return 'This booking cannot be cancelled in its current state or after departure. Refresh its status.';
    if(error.status===404||error.status===401)return classify(error);
    if(error.status===400)return 'The cancellation request is invalid. Refresh this booking.';
  }
  return 'The cancellation result is uncertain. Refresh this booking before retrying cancellation.';
}