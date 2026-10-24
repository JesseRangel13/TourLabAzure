import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Store } from '@ngxs/store';
import { catchError, switchMap, throwError } from 'rxjs';
import { ResetSession } from './auth.state';

export const sessionExpiryInterceptor: HttpInterceptorFn = (request, next) => {
  const store = inject(Store);
  return next(request).pipe(catchError((error: unknown) => {
    if (error instanceof HttpErrorResponse && error.status === 401 && !request.url.startsWith('/auth/')) {
      return store.dispatch(new ResetSession('Your session expired. Please sign in.')).pipe(
        switchMap(() => throwError(() => error)));
    }
    return throwError(() => error);
  }));
};
