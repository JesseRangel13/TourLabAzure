import { Component, inject } from '@angular/core';
import { Store } from '@ngxs/store';
import { AuthState } from './auth.state';

@Component({
  standalone: true,
  template: `<main><h1>My account</h1>
    @if (vm().user; as user) {
      <p>Signed in as <strong>{{ user.userName }}</strong>.</p><p>Role: {{ user.role }}</p>
    } @else { <p>Your session ended. Please sign in again.</p> }
    <p>This is an educational lab. Future payment, notification and confirmation effects are simulated.</p>
  </main>`
})
export class AccountPage {
  readonly vm = inject(Store).selectSignal(AuthState.view);
}
