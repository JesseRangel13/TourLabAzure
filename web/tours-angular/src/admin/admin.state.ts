import { Injectable, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { State, Action, Selector, StateContext } from '@ngxs/store';
import { catchError, finalize, tap, throwError, timeout } from 'rxjs';
import { UserSessionChanged } from '../auth/auth.state';
import { AdminApi, AdminCommand, DeadLetter, Diagnostics, Fault, Saga, Service } from './admin.api';
export class LoadOperations {static readonly type='[Admin] Diagnostics';constructor(readonly service:Service,readonly page=1){}}
export class LoadFault {static readonly type='[Admin] Fault';constructor(readonly service:Service){}}
export class ChangeFault {static readonly type='[Admin] Set fault';constructor(readonly service:Service,readonly mode:string,readonly occurrences:number,readonly reset=false){}}
export class LoadSagas {static readonly type='[Admin] Sagas';constructor(readonly filter='',readonly page=1){}}
export class InspectDlq {static readonly type='[Admin] Peek DLQ';constructor(readonly service:Service,readonly source:'commands'|'events',readonly from=0){}}
export class RunAdminCommand {static readonly type='[Admin] Mutate';constructor(readonly command:AdminCommand){}}
export interface AdminModel { service:Service;diagnostics:Diagnostics|null;fault:Fault|null;sagas:Saga[];deadLetters:DeadLetter[];
 loading:boolean;brokerLoading:boolean;mutating:boolean;error:string|null;brokerError:string|null;outcome:string|null; }
const initial=():AdminModel=>({service:'catalog',diagnostics:null,fault:null,sagas:[],deadLetters:[],loading:false,brokerLoading:false,mutating:false,error:null,brokerError:null,outcome:null});
@State<AdminModel>({name:'admin',defaults:initial()})
@Injectable()
export class AdminState {
 private readonly api=inject(AdminApi);private generation=0;private query=0;private peek=0;
 @Selector() static view(state:AdminModel){return state;}
 @Action(UserSessionChanged) reset(ctx:StateContext<AdminModel>){++this.generation;++this.query;++this.peek;ctx.setState(initial());}
 @Action(LoadOperations,{cancelUncompleted:true})
 operations(ctx:StateContext<AdminModel>,a:LoadOperations){
  const generation=this.generation,query=++this.query;++this.peek;
  const current=()=>generation===this.generation&&query===this.query;
  ctx.patchState({service:a.service,diagnostics:null,deadLetters:[],brokerLoading:false,loading:true,error:null,brokerError:null,outcome:null});
  return this.api.diagnostics(a.service,a.page).pipe(tap(diagnostics=>{if(current())ctx.patchState({diagnostics});}),
   catchError(e=>{if(current())ctx.patchState({error:message(e)});return throwError(()=>e);}),finalize(()=>{if(current())ctx.patchState({loading:false});}));
 }
 @Action(LoadFault,{cancelUncompleted:true})
 fault(ctx:StateContext<AdminModel>,a:LoadFault){
  const generation=this.generation,query=++this.query;const current=()=>generation===this.generation&&query===this.query;
  ctx.patchState({service:a.service,fault:null,loading:true,error:null,outcome:null});
  return this.api.fault(a.service).pipe(tap(fault=>{if(current())ctx.patchState({fault});}),catchError(e=>{if(current())ctx.patchState({error:message(e)});return throwError(()=>e);}),finalize(()=>{if(current())ctx.patchState({loading:false});}));
 }
 @Action(LoadSagas,{cancelUncompleted:true})
 sagas(ctx:StateContext<AdminModel>,a:LoadSagas){
  const generation=this.generation,query=++this.query;const current=()=>generation===this.generation&&query===this.query;
  ctx.patchState({sagas:[],loading:true,error:null,outcome:null});
  return this.api.sagas(a.filter,a.page).pipe(tap(sagas=>{if(current())ctx.patchState({sagas});}),catchError(e=>{if(current())ctx.patchState({error:message(e)});return throwError(()=>e);}),finalize(()=>{if(current())ctx.patchState({loading:false});}));
 }
 @Action(InspectDlq,{cancelUncompleted:true})
 inspect(ctx:StateContext<AdminModel>,a:InspectDlq){
  const generation=this.generation,query=++this.peek;const current=()=>generation===this.generation&&query===this.peek&&a.service===ctx.getState().service;
  ctx.patchState({deadLetters:[],brokerLoading:true,brokerError:null});
  return this.api.inspect(a.service,a.source,a.from).pipe(tap(deadLetters=>{if(current())ctx.patchState({deadLetters});}),
   catchError(e=>{if(current())ctx.patchState({brokerError:message(e)});return throwError(()=>e);}),finalize(()=>{if(current())ctx.patchState({brokerLoading:false});}));
 }
 @Action(ChangeFault)
 change(ctx:StateContext<AdminModel>,a:ChangeFault){
  if(ctx.getState().mutating)return throwError(()=>new Error('An Admin operation is pending.'));
  const generation=this.generation;ctx.patchState({mutating:true,error:null,outcome:null});
  return (a.reset?this.api.resetFault(a.service):this.api.setFault(a.service,a.mode,a.occurrences)).pipe(
   tap(fault=>{if(generation===this.generation && a.service===ctx.getState().service)ctx.patchState({fault,outcome:'Simulated fault selection saved.'});}),
   catchError(e=>{if(generation===this.generation)ctx.patchState({error:message(e)});return throwError(()=>e);}),finalize(()=>{if(generation===this.generation)ctx.patchState({mutating:false});}));
 }
 @Action(RunAdminCommand)
 run(ctx:StateContext<AdminModel>,a:RunAdminCommand){
  if(ctx.getState().mutating)return throwError(()=>new Error('An Admin operation is pending.'));
  const generation=this.generation;ctx.patchState({mutating:true,error:null,outcome:null});
  return this.api.run(a.command).pipe(timeout(28_000),tap(()=>{if(generation===this.generation)ctx.patchState({outcome:'Operation accepted. Refresh authoritative state; simulated effects remain asynchronous.'});}),
   catchError(e=>{if(generation===this.generation)ctx.patchState({error:message(e)});return throwError(()=>e);}),finalize(()=>{if(generation===this.generation)ctx.patchState({mutating:false});}));
 }
}
function message(error:unknown){
 if(error instanceof HttpErrorResponse){
  if(error.status===403)return 'Admin permission is required.';
  if(error.status===404)return 'This resource or local lab feature is unavailable.';
  if(error.status===409)return 'State changed or the operation is not safe yet. Refresh and review pending work.';
  if(error.status===400)return 'Check the requested operation and bounds.';
  if(error.status===429)return 'The Admin request limit was reached. Retry later.';
 }
 return 'The result is uncertain or a dependency is unavailable. Refresh before retrying; operation identifiers are preserved.';
}
