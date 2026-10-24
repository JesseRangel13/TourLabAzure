import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { timeout } from 'rxjs';
export interface BookingInput { sessionId: string; participants: number; expectedUnitAmountMinor: number; expectedCurrency: string; }
export interface Accepted { reservationId: string; status: string; sagaId: string; }
export interface Reservation {
  id: string; userId: string; sessionId: string; participants: number; unitAmountMinor: number;
  totalAmountMinor: number; currency: string; startsAtUtc: string; status: string; reason: string | null; version: number;
}
export interface ReservationPage { items: Reservation[]; total: number; page: number; pageSize: number; }
export interface Transition { from: string; to: string; reason: string | null; occurredAtUtc: string; }
export interface Payment { id: string; reservationId: string; amountMinor: number; currency: string; status: string; simulated: boolean; }
export interface Notification { id: string; reservationId: string; subject: string; body: string; status: string; simulated: boolean; }
export interface NotificationPage { items: Notification[]; total: number; page: number; pageSize: number; }
@Injectable({ providedIn: 'root' })
export class ReservationsApi {
  private readonly http = inject(HttpClient);
  create(input: BookingInput, key: string) {
    return this.http.post<Accepted>('/api/reservations/v1/reservations', input,
      { headers: new HttpHeaders().set('Idempotency-Key', key) }).pipe(timeout(12_000));
  }
  cancel(id:string,key:string){return this.http.post<Accepted>("/api/reservations/v1/reservations/"+id+"/cancel",{}, {headers:new HttpHeaders().set("Idempotency-Key",key)}).pipe(timeout(12_000));}
  list(page: number, status="", userId="") {
    return this.http.get<ReservationPage>('/api/reservations/v1/reservations', { params: { page, pageSize: 10, ...(status?{status}:{}), ...(userId?{userId}:{}) } }).pipe(timeout(10_000));
  }
  detail(id: string) { return this.http.get<Reservation>('/api/reservations/v1/reservations/' + id).pipe(timeout(10_000)); }
  timeline(id: string) { return this.http.get<Transition[]>('/api/reservations/v1/reservations/' + id + '/timeline').pipe(timeout(10_000)); }
  payments(id?: string, page = 1) {
    return this.http.get<Payment[]>('/api/payments/v1/payments', { params: id ? new HttpParams().set('reservationId', id).set('page', page) : new HttpParams().set('page', page) }).pipe(timeout(10_000));
  }
  notifications(id?: string, page = 1) {
    return this.http.get<NotificationPage>('/api/notifications/v1/notifications', { params: id ? new HttpParams().set('reservationId', id).set('page', page) : new HttpParams().set('page', page) }).pipe(timeout(10_000));
  }
}