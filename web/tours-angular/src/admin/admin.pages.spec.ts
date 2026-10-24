import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideStore } from '@ngxs/store';
import { AdminState } from './admin.state';
import { OperationsPage, SagasPage, SimulationsPage } from './admin.pages';
describe('Admin controls',()=>{
 beforeEach(()=>TestBed.configureTestingModule({imports:[OperationsPage,SagasPage,SimulationsPage],providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([]),provideStore([AdminState])]}));
 afterEach(()=>TestBed.inject(HttpTestingController).verify());
 it('renders bounded diagnostics and disables replay for an unsafe inspected envelope',()=>{
  const fixture=TestBed.createComponent(OperationsPage),http=TestBed.inject(HttpTestingController);
  http.expectOne(r=>r.url.endsWith('diagnostics')).flush({service:'catalog',pendingOutboxCount:2,oldestPendingOutboxAgeSeconds:4,ignoredDuplicates:1,states:{},workers:[],outbox:[],inbox:[],audit:[]});
  fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Pending Outbox: 2');
  fixture.componentInstance.peek(true);http.expectOne(r=>r.url.endsWith('dead-letters')).flush([{id:'bad',source:'commands',sequenceNumber:1,type:'InvalidEnvelope',reason:'ClassifiedPoison',replayable:false}]);
  fixture.detectChanges();const replay=Array.from(fixture.nativeElement.querySelectorAll('button')).find((b:any)=>b.textContent.includes('Replay inspected')) as HTMLButtonElement;
  expect(replay.disabled).toBe(true);expect(fixture.nativeElement.textContent).toContain('Peek does not lock');
 });
 it('validates integer bounded fault occurrences and sends only a provider resolution',()=>{
  const fixture=TestBed.createComponent(SimulationsPage),http=TestBed.inject(HttpTestingController);
  http.expectOne(r=>r.url.endsWith('faults')).flush({mode:'None',remaining:0});fixture.detectChanges();
  for(const value of [-1,101,1.5]){fixture.componentInstance.faultForm.controls.occurrences.setValue(value);expect(fixture.componentInstance.faultForm.invalid).toBe(true);fixture.componentInstance.save();}
  http.expectNone(r=>r.method==='PUT');
  fixture.componentInstance.operationControl.setValue('11223344-5566-7788-9900-112233445566');fixture.componentInstance.outcomeControl.setValue('NotCharged');
  fixture.componentInstance.resolve();const request=http.expectOne(r=>r.url.endsWith('/resolve'));expect(request.request.body).toEqual({outcome:'NotCharged'});request.flush({},{status:202,statusText:'Accepted'});
  http.expectNone(r=>r.url.endsWith('/reconcile'));fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Fictional providers only');
 });
 it('permits unpaid release repair and blocks only an unresolved requested payment',()=>{
  const fixture=TestBed.createComponent(SagasPage),http=TestBed.inject(HttpTestingController);
  const saga={id:'saga',reservationId:'booking',state:'ManualReview',paymentOperationId:'payment',refundOperationId:'refund',paymentResolved:false,paymentRequested:true,refundRequired:false,refundCompleted:false,seatsReleaseRequired:true,seatsReleased:true,reason:'PaymentOutcomeUnresolved'};
  http.expectOne(r=>r.url.endsWith('sagas')).flush([saga,{...saga,id:'unpaid',paymentRequested:false,reason:'AvailabilityDeadlineExceeded'}]);
  fixture.detectChanges();const buttons=Array.from(fixture.nativeElement.querySelectorAll('button')).filter((b:any)=>b.textContent.includes('Retry pending compensation')) as HTMLButtonElement[];
  expect(buttons[0].disabled).toBe(true);expect(buttons[1].disabled).toBe(false);expect(fixture.nativeElement.textContent).toContain('PaymentOutcomeUnresolved');
 });
});
