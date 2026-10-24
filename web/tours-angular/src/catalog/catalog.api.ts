import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { timeout } from 'rxjs';
export interface Tour { id: string; name: string; description: string; active: boolean; version: number; }
export interface Session {
  id: string; tourId: string; startsAtUtc: string; unitAmountMinor: number; currency: string;
  capacity: number; availableSeats: number; priceVersion: number; version: number;
}
export interface TourPage { items: Tour[]; total: number; page: number; pageSize: number; }
export interface TourInput { name: string; description: string; }
export interface SessionInput { startsAtUtc: string; unitAmountMinor: number; capacity: number; }
@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);
  private readonly root = '/api/catalog/v1';
  tours(search: string, page: number) {
    return this.http.get<TourPage>(this.root + '/tours', {
      params: new HttpParams().set('search', search).set('page', page).set('pageSize', 10)
    }).pipe(timeout(10_000));
  }
  tour(id: string) { return this.http.get<Tour>(this.root + '/tours/' + id).pipe(timeout(10_000)); }
  sessions(id: string) { return this.http.get<Session[]>(this.root + '/tours/' + id + '/sessions').pipe(timeout(10_000)); }
  create(input: TourInput) { return this.http.post<Tour>(this.root + '/admin/tours', input).pipe(timeout(10_000)); }
  update(id: string, input: TourInput & { active: boolean; expectedVersion: number }) {
    return this.http.put<Tour>(this.root + '/admin/tours/' + id, input).pipe(timeout(10_000));
  }
  createSession(id: string, input: SessionInput) {
    return this.http.post<Session>(this.root + '/admin/tours/' + id + '/sessions', input).pipe(timeout(10_000));
  }
  price(id: string, unitAmountMinor: number, expectedVersion: number) {
    return this.http.put<Session>(this.root + '/admin/sessions/' + id + '/price', { unitAmountMinor, expectedVersion }).pipe(timeout(10_000));
  }
  capacity(id: string, capacity: number, expectedVersion: number) {
    return this.http.put<Session>(this.root + '/admin/sessions/' + id + '/capacity', { capacity, expectedVersion }).pipe(timeout(10_000));
  }
}