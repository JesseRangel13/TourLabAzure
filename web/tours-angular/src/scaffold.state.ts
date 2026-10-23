import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Action, Selector, State, StateContext } from '@ngxs/store';
import { catchError, finalize, tap, throwError } from 'rxjs';

export class LoadGatewayStatus { static readonly type = '[Scaffold] Load gateway status'; }
export interface ScaffoldModel { loading: boolean; status: string; error: string | null; }

@State<ScaffoldModel>({
  name: 'scaffold',
  defaults: { loading: false, status: 'Not checked', error: null }
})
@Injectable()
export class ScaffoldState {
  private readonly http = inject(HttpClient);
  @Selector() static view(state: ScaffoldModel) { return state; }

  @Action(LoadGatewayStatus, { cancelUncompleted: true })
  load(ctx: StateContext<ScaffoldModel>) {
    ctx.patchState({ loading: true, error: null });
    return this.http.get<{ status: string }>('/health/live').pipe(
      tap(result => ctx.patchState({ status: result.status })),
      catchError((error: HttpErrorResponse) => {
        ctx.patchState({ status: 'Unavailable', error: error.status === 0 ? 'Gateway could not be reached.' : 'Gateway check failed.' });
        return throwError(() => error);
      }),
      finalize(() => ctx.patchState({ loading: false }))
    );
  }
}
