import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { timeout } from 'rxjs';

export interface CurrentUser { userId: string; userName: string; role: 'Tourist' | 'Admin'; }
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  csrf() { return this.http.get<{ requestToken: string }>('/auth/csrf').pipe(timeout(10_000)); }
  me() { return this.http.get<CurrentUser>('/auth/me').pipe(timeout(10_000)); }
  login(userName: string, password: string) {
    return this.http.post<CurrentUser>('/auth/login', { userName, password }).pipe(timeout(10_000));
  }
  logout() { return this.http.post<void>('/auth/logout', {}).pipe(timeout(10_000)); }
}
