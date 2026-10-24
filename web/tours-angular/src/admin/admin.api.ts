import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { timeout } from 'rxjs';
export type Service = 'catalog' | 'reservations' | 'payments' | 'notifications';
export const services: Service[] = ['catalog', 'reservations', 'payments', 'notifications'];
export const modes: Record<Service, string[]> = {
 catalog: ['None','QuoteTimeout','RejectNextConfirmation','PauseConsumption'], reservations: ['None','PauseOutbox'],
 payments: ['Success','Decline','TimeoutAfterCharge','UnknownUntilAdminResolution','RefundTransientFailure','PauseConsumption'],
 notifications: ['None','FailureThenSuccess']
};
export interface Fault { mode: string; remaining: number; }
export interface Saga { id: string; reservationId: string; state: string; paymentOperationId: string; refundOperationId: string;
 refundRequired: boolean; refundCompleted: boolean; seatsReleaseRequired: boolean; seatsReleased: boolean; paymentResolved: boolean; paymentRequested: boolean; reason: string | null; }
export interface Diagnostics { service: string; pendingOutboxCount: number; oldestPendingOutboxAgeSeconds: number; ignoredDuplicates: number;
 states: Record<string,number>; workers: { name:string; status:string; lastSuccessUtc:string|null; lastFailureType:string|null }[];
 outbox: { messageId:string; type:string; attempts:number }[]; inbox:{consumerName:string;messageId:string}[]; audit:{actor:string;action:string;occurredAtUtc:string}[]; }
export interface DeadLetter { id:string; source:string; sequenceNumber:number; logicalMessageId:string|null; type:string; sagaId:string|null; reason:string; replayable:boolean; }
export type AdminCommand = { kind:'retry';id:string } | {kind:'resolve';id:string;outcome:'Succeeded'|'NotCharged'} |
 {kind:'reconcile';id:string} | {kind:'replay';id:string;service:Service};
@Injectable({providedIn:'root'})
export class AdminApi {
 private readonly http=inject(HttpClient);
 diagnostics(service:Service,page:number){return this.http.get<Diagnostics>('/api/'+service+'/v1/admin/diagnostics',{params:{page,pageSize:10}}).pipe(timeout(12_000));}
 fault(service:Service){return this.http.get<Fault>('/api/'+service+'/v1/admin/faults').pipe(timeout(10_000));}
 setFault(service:Service,mode:string,occurrences:number){return this.http.put<Fault>('/api/'+service+'/v1/admin/faults',{mode,occurrences}).pipe(timeout(10_000));}
 resetFault(service:Service){return this.http.post<Fault>('/api/'+service+'/v1/admin/faults/reset',{}).pipe(timeout(10_000));}
 sagas(state:string,page:number){return this.http.get<Saga[]>('/api/reservations/v1/admin/sagas',{params:state?{state,page}:{page}}).pipe(timeout(10_000));}
 inspect(service:Service,source:'commands'|'events',from:number){return this.http.get<DeadLetter[]>('/api/'+service+'/v1/admin/dead-letters',{params:{source,pageSize:10,fromSequence:from}}).pipe(timeout(22_000));}
 run(command:AdminCommand){
  switch(command.kind){
   case 'retry':return this.http.post('/api/reservations/v1/admin/sagas/'+command.id+'/retry-compensation',{});
   case 'resolve':return this.http.post('/api/payments/v1/admin/fake-provider/'+command.id+'/resolve',{outcome:command.outcome});
   case 'reconcile':return this.http.post('/api/payments/v1/admin/payments/'+command.id+'/reconcile',{});
   case 'replay':return this.http.post('/api/'+command.service+'/v1/admin/dead-letters/'+command.id+'/replay',{});
  }
 }
}
