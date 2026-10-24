import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngxs/store';
import { finalize } from 'rxjs';
import { AuthState, Login } from './auth.state';

@Component({
  standalone: true, imports: [ReactiveFormsModule],
  template: `<main><h1>Sign in</h1><p>Use your educational lab account.</p>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <label for="userName">User name</label>
      <input id="userName" formControlName="userName" autocomplete="username" maxlength="100">
      <label for="password">Password</label>
      <input id="password" formControlName="password" type="password" autocomplete="current-password" maxlength="256">
      <button type="submit" [disabled]="form.invalid || vm().loading">Sign in</button>
    </form>
    @if (vm().loading) { <p role="status">Signing in…</p> }
    @if (vm().error) { <p role="alert">{{ vm().error }}</p> }
  </main>`
})
export class LoginPage {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly vm = this.store.selectSignal(AuthState.view);
  readonly form = inject(FormBuilder).nonNullable.group({
    userName: ['', [Validators.required, Validators.maxLength(100)]],
    password: ['', [Validators.required, Validators.maxLength(256)]]
  });
  submit() {
    if (this.form.invalid || this.vm().loading) return;
    const { userName, password } = this.form.getRawValue();
    this.store.dispatch(new Login(userName, password)).pipe(finalize(() => this.form.controls.password.reset())).subscribe({
      next: () => {
        const target = this.route.snapshot.queryParamMap.get('returnUrl');
        void this.router.navigateByUrl(target?.startsWith('/') && !target.startsWith('//') ? target : '/account');
      },
      error: () => {}
    });
  }
}
