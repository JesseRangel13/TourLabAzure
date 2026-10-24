import { Component, inject, provideAppInitializer } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { provideRouter, Router, RouterLink, RouterOutlet } from '@angular/router';
import { provideStore, Store } from '@ngxs/store';
import { firstValueFrom } from 'rxjs';
import { LoadGatewayStatus, ScaffoldState } from './scaffold.state';
import { AuthState, LoadCurrentUser, Logout } from './auth/auth.state';
import { sessionExpiryInterceptor } from './auth/session.interceptor';
import { authGuard } from './auth/auth.guard';
import { LoginPage } from './auth/login.page';
import { AccountPage } from './auth/account.page';

@Component({
  standalone: true,
  template: `<main>
    <p>EDUCATIONAL LAB · AZ-002</p>
    <h1>TourLab Azure</h1>
    <p>An independent Angular 21 and .NET 10 microservices companion.</p>
    <p>Sign in to inspect your lab account. Booking, payment and receipt workflows arrive in later tasks.
       All payments, notifications and booking confirmations are simulated.</p>
    <section class="status" aria-live="polite">
      <h2>Gateway process</h2>
      <p>{{ vm().loading ? 'Checking…' : vm().status }}</p>
      @if (vm().error) { <p role="alert">{{ vm().error }}</p> }
      <button (click)="check()" [disabled]="vm().loading">Check gateway</button>
    </section>
    <p>Process liveness does not establish workflow readiness.</p>
  </main>`
})
class FoundationPage {
  private readonly store = inject(Store);
  readonly vm = this.store.selectSignal(ScaffoldState.view);
  check() { this.store.dispatch(new LoadGatewayStatus()).subscribe({ error: () => {} }); }
}

@Component({
  selector: 'app-root', standalone: true, imports: [RouterOutlet, RouterLink],
  template: `<nav aria-label="Main navigation"><a routerLink="/">Home</a>
    @if (auth().user) {
      <a routerLink="/account">My account</a><button (click)="logout()" [disabled]="auth().loading">Sign out</button>
    } @else { <a routerLink="/login">Sign in</a> }
  </nav>
  @if (auth().error) { <p role="alert" class="session-message">{{ auth().error }}</p> }
  <router-outlet />`
})
class App {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  readonly auth = this.store.selectSignal(AuthState.view);
  logout() {
    this.store.dispatch(new Logout()).subscribe({ next: () => { void this.router.navigateByUrl('/login'); }, error: () => {} });
  }
}

bootstrapApplication(App, {
  providers: [
    provideHttpClient(withXsrfConfiguration({ cookieName: 'AZURELAB-XSRF-TOKEN', headerName: 'X-AZURELAB-XSRF-TOKEN' }), withInterceptors([sessionExpiryInterceptor])),
    provideStore([ScaffoldState, AuthState]),
    provideRouter([{ path: '', component: FoundationPage }, { path: 'login', component: LoginPage },
      { path: 'account', component: AccountPage, canActivate: [authGuard] }, { path: '**', redirectTo: '' }]),
    provideAppInitializer(() => firstValueFrom(inject(Store).dispatch(new LoadCurrentUser())).catch(() => undefined))
  ]
}).catch(() => console.error('Angular bootstrap failed'));
