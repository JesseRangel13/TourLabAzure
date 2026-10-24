import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Store } from '@ngxs/store';
import { catchError, map, of } from 'rxjs';
import { AuthState, LoadCurrentUser } from './auth.state';

export const authGuard: CanActivateFn = (_, route) => {
  const store = inject(Store);
  const router = inject(Router);
  const result = () => store.selectSnapshot(AuthState.view).user !== null ||
    router.createUrlTree(['/login'], { queryParams: { returnUrl: route.url } });
  return store.dispatch(new LoadCurrentUser()).pipe(map(result), catchError(() => of(router.createUrlTree(['/login']))));
};
