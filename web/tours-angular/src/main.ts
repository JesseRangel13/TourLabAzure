import { ReceiptsState } from './receipts/receipts.state';
import { DashboardState } from './dashboard/dashboard.state';
import { DashboardPageComponent, DashboardDetailComponent } from './dashboard/dashboard.pages';
import { ImagesState } from './catalog/images.state';
import { AdminState } from './admin/admin.state';
import { SagasPage, SimulationsPage, OperationsPage } from './admin/admin.pages';
import { EffectsState, EffectDetailPage, ProjectionsPage } from './reservations/effects.state';
import { ReservationsState } from "./reservations/reservations.state";
import { ReservationsPage, ReservationDetailPage, PersonalEffectsPage } from "./reservations/reservations.pages";
import { adminGuard } from "./catalog/admin.guard";
import { CatalogState } from "./catalog/catalog.state";
import { CatalogListPage, CatalogDetailPage } from "./catalog/catalog.pages";
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
    <p>EDUCATIONAL LAB</p>
    <h1>TourLab Azure</h1>
    <p>An independent Angular 21 and .NET 10 microservices companion.</p>
    <p>Sign in to browse tours, sessions and availability. Booking and simulated payment/notification workflows are available locally. Historical simulated confirmations are available on booking details.
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
      <a routerLink="/tours">Tours</a><a routerLink="/tour-projections">Informational prices</a><a routerLink="/dashboard">My booking dashboard</a><a routerLink="/reservations">Authoritative bookings</a><a routerLink="/payments">Payments</a><a routerLink="/notifications">Notifications</a>@if (auth().user?.role === 'Admin') { <a routerLink="/admin/catalog">Admin catalog</a><a routerLink="/admin/sagas">Sagas</a><a routerLink="/simulations">Simulations</a><a routerLink="/operations">Operations</a> }<a routerLink="/account">My account</a><button (click)="logout()" [disabled]="auth().loading">Sign out</button>
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
    provideStore([ReceiptsState,ScaffoldState, AuthState, CatalogState, ReservationsState, AdminState, EffectsState, ImagesState, DashboardState]),
    provideRouter([{path:'dashboard',component:DashboardPageComponent,canActivate:[authGuard]},{path:'dashboard/:id',component:DashboardDetailComponent,canActivate:[authGuard]}, { path: '', component: FoundationPage }, { path: 'login', component: LoginPage },
      {path:'admin/sagas',component:SagasPage,canActivate:[authGuard,adminGuard]}, {path:'simulations',component:SimulationsPage,canActivate:[authGuard,adminGuard]}, {path:'operations',component:OperationsPage,canActivate:[authGuard,adminGuard]}, {path:'tour-projections',component:ProjectionsPage,canActivate:[authGuard]}, {path:'payments/:id',component:EffectDetailPage,canActivate:[authGuard],data:{kind:'payments'}}, {path:'notifications/:id',component:EffectDetailPage,canActivate:[authGuard],data:{kind:'notifications'}}, { path: 'reservations', component: ReservationsPage, canActivate: [authGuard] }, { path: 'reservations/:id', component: ReservationDetailPage, canActivate: [authGuard] }, { path: 'payments', component: PersonalEffectsPage, canActivate: [authGuard], data: { kind: 'payments' } }, { path: 'notifications', component: PersonalEffectsPage, canActivate: [authGuard], data: { kind: 'notifications' } }, { path: 'tours', component: CatalogListPage, canActivate: [authGuard] }, { path: 'tours/:id', component: CatalogDetailPage, canActivate: [authGuard] }, { path: 'admin/catalog', component: CatalogListPage, canActivate: [authGuard, adminGuard] }, { path: 'account', component: AccountPage, canActivate: [authGuard] }, { path: '**', redirectTo: '' }]),
    provideAppInitializer(() => firstValueFrom(inject(Store).dispatch(new LoadCurrentUser())).catch(() => undefined))
  ]
}).catch(() => console.error('Angular bootstrap failed'));
