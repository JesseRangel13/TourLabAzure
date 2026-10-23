import { Component, inject } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, RouterOutlet } from '@angular/router';
import { provideStore, Store } from '@ngxs/store';
import { LoadGatewayStatus, ScaffoldState } from './scaffold.state';

@Component({
  standalone: true,
  template: `<main>
    <p>EDUCATIONAL LAB · AZ-001</p>
    <h1>TourLab Azure</h1>
    <p>An independent Angular 21 and .NET 10 microservices companion.</p>
    <p>Foundation scaffold. Booking, login, payments and receipt workflows are scheduled in later tasks.
       All future payments, notifications and booking confirmations are simulated.</p>
    <section class="status" aria-live="polite">
      <h2>Gateway process</h2>
      <p>{{ vm().loading ? 'Checking…' : vm().status }}</p>
      @if (vm().error) { <p role="alert">{{ vm().error }}</p> }
      <button (click)="check()" [disabled]="vm().loading">Check gateway</button>
    </section>
    <p>Process liveness does not establish database, broker or workflow readiness.</p>
  </main>`
})
class FoundationPage {
  private readonly store = inject(Store);
  readonly vm = this.store.selectSignal(ScaffoldState.view);
  check() { this.store.dispatch(new LoadGatewayStatus()).subscribe({ error: () => {} }); }
}

@Component({ selector: 'app-root', standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
class App {}

bootstrapApplication(App, {
  providers: [provideHttpClient(), provideStore([ScaffoldState]), provideRouter([
    { path: '', component: FoundationPage },
    { path: '**', redirectTo: '' }
  ])]
}).catch(error => console.error('Angular bootstrap failed', error));
