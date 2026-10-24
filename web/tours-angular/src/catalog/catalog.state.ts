import { inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Action, Selector, State, StateContext } from '@ngxs/store';
import { catchError, finalize, forkJoin, Observable, tap, throwError } from 'rxjs';
import { UserSessionChanged } from '../auth/auth.state';
import { CatalogApi, Session, SessionInput, Tour, TourInput, TourPage } from './catalog.api';
export class LoadTours { static readonly type = '[Catalog] Load tours'; constructor(readonly search = '', readonly page = 1) {} }
export class LoadCatalogTour { static readonly type = '[Catalog] Load tour'; constructor(readonly id: string) {} }
export class SaveTour {
  static readonly type = '[Catalog] Save tour';
  constructor(readonly input: TourInput, readonly existing?: Tour, readonly active = true) {}
}
export class CreateCatalogSession {
  static readonly type = '[Catalog] Create session';
  constructor(readonly tourId: string, readonly input: SessionInput) {}
}
export class ChangeSessionPrice { static readonly type = '[Catalog] Change price'; constructor(readonly session: Session, readonly amount: number) {} }
export class ChangeSessionCapacity { static readonly type = '[Catalog] Change capacity'; constructor(readonly session: Session, readonly capacity: number) {} }
export interface CatalogModel {
  page: TourPage | null; search: string; tour: Tour | null; sessions: Session[];
  listLoading: boolean; detailLoading: boolean; saving: boolean; error: string | null;
}
const defaults = (): CatalogModel => ({
  page: null, search: '', tour: null, sessions: [], listLoading: false, detailLoading: false, saving: false, error: null
});
@State<CatalogModel>({ name: 'catalog', defaults: defaults() })
@Injectable()
export class CatalogState {
  private readonly api = inject(CatalogApi);
  private generation = 0;
  private listRequest = 0;
  private detailRequest = 0;
  @Selector() static view(state: CatalogModel) { return state; }
  @Selector() static tour(state: CatalogModel) { return state.tour; }
  @Selector() static tours(state: CatalogModel) { return state.page?.items ?? []; }
  @Action(UserSessionChanged)
  reset(ctx: StateContext<CatalogModel>) {
    ++this.generation; ++this.listRequest; ++this.detailRequest; ctx.setState(defaults());
  }
  @Action(LoadTours, { cancelUncompleted: true })
  load(ctx: StateContext<CatalogModel>, action: LoadTours) {
    const generation = this.generation, request = ++this.listRequest;
    const current = () => generation === this.generation && request === this.listRequest;
    ctx.patchState({ listLoading: true, error: null, search: action.search, page: null });
    return this.api.tours(action.search, action.page).pipe(
      tap(page => { if (current()) ctx.patchState({ page }); }),
      catchError((error: unknown) => { if (current()) ctx.patchState({ error: classify(error) }); return throwError(() => error); }),
      finalize(() => { if (current()) ctx.patchState({ listLoading: false }); })
    );
  }
  @Action(LoadCatalogTour, { cancelUncompleted: true })
  detail(ctx: StateContext<CatalogModel>, action: LoadCatalogTour) {
    const generation = this.generation, request = ++this.detailRequest;
    const current = () => generation === this.generation && request === this.detailRequest;
    ctx.patchState({ detailLoading: true, error: null, tour: null, sessions: [] });
    return forkJoin({ tour: this.api.tour(action.id), sessions: this.api.sessions(action.id) }).pipe(
      tap(value => { if (current()) ctx.patchState(value); }),
      catchError((error: unknown) => { if (current()) ctx.patchState({ error: classify(error) }); return throwError(() => error); }),
      finalize(() => { if (current()) ctx.patchState({ detailLoading: false }); })
    );
  }
  @Action(SaveTour)
  save(ctx: StateContext<CatalogModel>, action: SaveTour) {
    return this.mutate(ctx, () => action.existing
      ? this.api.update(action.existing.id, { ...action.input, active: action.active, expectedVersion: action.existing.version })
      : this.api.create(action.input), tour => {
        if (ctx.getState().tour?.id === tour.id) ctx.patchState({ tour, sessions: [] });
        ctx.patchState({ page: null });
      });
  }
  @Action(CreateCatalogSession)
  createSession(ctx: StateContext<CatalogModel>, action: CreateCatalogSession) {
    return this.mutate(ctx, () => this.api.createSession(action.tourId, action.input), session => this.session(ctx, session));
  }
  @Action(ChangeSessionPrice)
  price(ctx: StateContext<CatalogModel>, action: ChangeSessionPrice) {
    return this.mutate(ctx, () => this.api.price(action.session.id, action.amount, action.session.version), session => this.session(ctx, session));
  }
  @Action(ChangeSessionCapacity)
  capacity(ctx: StateContext<CatalogModel>, action: ChangeSessionCapacity) {
    return this.mutate(ctx, () => this.api.capacity(action.session.id, action.capacity, action.session.version), session => this.session(ctx, session));
  }
  private session(ctx: StateContext<CatalogModel>, session: Session) {
    if (ctx.getState().tour?.id !== session.tourId) return;
    const sessions = ctx.getState().sessions.filter(value => value.id !== session.id).concat(session);
    sessions.sort((a, b) => a.startsAtUtc.localeCompare(b.startsAtUtc) || a.id.localeCompare(b.id));
    ctx.patchState({ sessions });
  }
  private mutate<T>(ctx: StateContext<CatalogModel>, request: () => Observable<T>, apply: (value: T) => void) {
    if (ctx.getState().saving) return throwError(() => new Error('A catalog change is already in progress.'));
    const generation = this.generation;
    ++this.listRequest; ++this.detailRequest;
    ctx.patchState({ saving: true, error: null, listLoading: false, detailLoading: false });
    return request().pipe(
      tap(value => { if (generation === this.generation) apply(value); }),
      catchError((error: unknown) => {
        if (generation === this.generation) ctx.patchState({ error: classify(error) }); return throwError(() => error);
      }),
      finalize(() => { if (generation === this.generation) ctx.patchState({ saving: false }); })
    );
  }
}
function classify(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 409) return 'This record changed or its capacity is occupied. Reload before editing again.';
    if (error.status === 404) return 'This tour is unavailable.';
    if (error.status === 403) return 'Administrator access is required.';
    if (error.status === 401) return 'Your session expired. Sign in again.';
    if (error.status === 400) return 'Check the supplied values and try again.';
  }
  return 'Catalog is unavailable. Please retry.';
}