import { inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Action, Selector, State, StateContext } from '@ngxs/store';
import { catchError, finalize, map, of, switchMap, tap, throwError } from 'rxjs';
import { AuthApi, CurrentUser } from './auth.api';

export class LoadCurrentUser { static readonly type = '[Auth] Load current user'; }
export class Login {
  static readonly type = '[Auth] Login';
  // Transient action input, never state/persistence/logging; scrub after request completion.
  constructor(readonly userName: string, public password: string) {}
}
export class Logout { static readonly type = '[Auth] Logout'; }
export class ResetSession {
  static readonly type = '[Auth] Reset session';
  constructor(readonly message: string | null = null) {}
}
export class UserSessionChanged { static readonly type = '[Auth] User session changed'; }
export interface AuthModel { user: CurrentUser | null; initialized: boolean; loading: boolean; error: string | null; }

@Injectable({ providedIn: 'root' })
export class SessionBoundary {
  private generation = 0;
  advance() { return ++this.generation; }
  current(value: number) { return value === this.generation; }
}

@State<AuthModel>({ name: 'auth', defaults: { user: null, initialized: false, loading: false, error: null } })
@Injectable()
export class AuthState {
  private readonly api = inject(AuthApi);
  private readonly boundary = inject(SessionBoundary);
  private mutating = false;
  @Selector() static view(state: AuthModel) { return state; }

  @Action(ResetSession)
  reset(ctx: StateContext<AuthModel>, action: ResetSession) {
    this.boundary.advance();
    ctx.setState({ user: null, initialized: true, loading: false, error: action.message });
    return ctx.dispatch(new UserSessionChanged());
  }

  @Action(LoadCurrentUser, { cancelUncompleted: true })
  load(ctx: StateContext<AuthModel>) {
    if (this.mutating) return of(undefined);
    const version = this.boundary.advance();
    ctx.patchState({ loading: true, error: null });
    return this.api.me().pipe(
      tap(user => {
        if (this.boundary.current(version)) {
          if (ctx.getState().user?.userId !== user.userId) ctx.dispatch(new UserSessionChanged());
          ctx.patchState({ user, initialized: true });
        }
      }),
      catchError((error: unknown) => {
        if (this.boundary.current(version)) {
          ctx.patchState({ user: null, initialized: true, error: status(error) === 401 ? null : 'Session could not be checked. Please retry.' });
          ctx.dispatch(new UserSessionChanged());
        }
        return status(error) === 401 ? of(undefined) : throwError(() => error);
      }),
      finalize(() => { if (this.boundary.current(version)) ctx.patchState({ loading: false }); })
    );
  }

  @Action(Login)
  login(ctx: StateContext<AuthModel>, action: Login) {
    if (this.mutating) { action.password = ''; return throwError(() => new Error('An authentication change is already in progress.')); }
    this.mutating = true;
    const version = this.boundary.advance();
    ctx.setState({ user: null, initialized: true, loading: true, error: null });
    ctx.dispatch(new UserSessionChanged());
    return this.api.csrf().pipe(
      switchMap(() => this.api.login(action.userName, action.password)),
      // Antiforgery tokens bind the authenticated identity; acquire a new one after login.
      switchMap(user => this.api.csrf().pipe(map(() => user))),
      tap(user => { if (this.boundary.current(version)) ctx.patchState({ user }); }),
      catchError((error: unknown) => {
        if (this.boundary.current(version)) ctx.patchState({ user: null, error: status(error) === 401 ? 'Invalid user name or password.' : 'Sign in could not be completed. Please retry.' });
        return throwError(() => error);
      }),
      finalize(() => {
        action.password = '';
        this.mutating = false;
        if (this.boundary.current(version)) ctx.patchState({ loading: false });
      })
    );
  }

  @Action(Logout)
  logout(ctx: StateContext<AuthModel>) {
    if (this.mutating) return throwError(() => new Error('An authentication change is already in progress.'));
    this.mutating = true;
    const version = this.boundary.advance();
    ctx.setState({ user: null, initialized: true, loading: true, error: null });
    ctx.dispatch(new UserSessionChanged());
    return this.api.csrf().pipe(
      switchMap(() => this.api.logout()),
      catchError((error: unknown) => {
        if (status(error) === 401) return of(undefined);
        if (this.boundary.current(version)) ctx.patchState({ error: 'Sign out could not be completed. Please retry.' });
        return throwError(() => error);
      }),
      finalize(() => {
        this.mutating = false;
        if (this.boundary.current(version)) ctx.patchState({ loading: false });
      })
    );
  }
}
function status(error: unknown) { return error instanceof HttpErrorResponse ? error.status : 0; }
