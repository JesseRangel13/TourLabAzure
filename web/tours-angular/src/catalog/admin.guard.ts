import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Store } from '@ngxs/store';
import { AuthState } from '../auth/auth.state';
export const adminGuard: CanActivateFn = () => inject(Store).selectSnapshot(AuthState.view).user?.role === 'Admin' ||
  inject(Router).createUrlTree(['/tours']);