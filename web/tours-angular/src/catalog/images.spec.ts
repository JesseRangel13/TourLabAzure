import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideStore, Store } from '@ngxs/store';
import { UserSessionChanged, AuthState } from '../auth/auth.state';
import { ImagesState, LoadImages, UploadImage, RemoveImage, RecoverImage, TourImage } from './images.state';
import { TourImagesPanel } from './images.panel';
describe('Private tour images',()=>{
  let store:Store,http:HttpTestingController;
  const image:TourImage={id:'i',tourId:'t',contentType:'image/png',byteSize:68,status:'Ready',version:2,url:'/api/catalog/v1/tours/t/images/i/content',attempts:1};
  beforeEach(()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),provideStore([AuthState,ImagesState])]});
    store=TestBed.inject(Store);http=TestBed.inject(HttpTestingController);
  });
  afterEach(()=>http.verify());
  function load(){store.dispatch(new LoadImages('t')).subscribe();http.expectOne('/api/catalog/v1/tours/t/images').flush([image]);}
  it('cancels stale loads and ignores responses from an earlier account',()=>{
    store.dispatch(new LoadImages('old')).subscribe();const old=http.expectOne('/api/catalog/v1/tours/old/images');
    store.dispatch(new LoadImages('t')).subscribe();expect(old.cancelled).toBe(true);
    const current=http.expectOne('/api/catalog/v1/tours/t/images');
    store.dispatch(new UserSessionChanged()).subscribe();current.flush([image]);
    expect(store.selectSnapshot(ImagesState.view).items).toEqual([]);
    expect(store.selectSnapshot(ImagesState.view).loading).toBe(false);
  });
  it('sends the raw file with a stable intention id and reloads durable status',()=>{
    load();const file=new File(['png'],'tour.png',{type:'image/png'});
    store.dispatch(new UploadImage('t','same-id',file)).subscribe();
    const put=http.expectOne('/api/catalog/v1/admin/tours/t/images/same-id');
    expect(put.request.method).toBe('PUT');expect(put.request.body).toBe(file);put.flush(image);
    http.expectOne('/api/catalog/v1/tours/t/images').flush([image]);
    expect(store.selectSnapshot(ImagesState.view).saving).toBe(false);
  });
  it('classifies server validation and storage failures without automatic mutation retry',()=>{
    load();const file=new File(['png'],'tour.png',{type:'image/png'});
    store.dispatch(new UploadImage('t','same-id',file)).subscribe({error:()=>{}});
    http.expectOne('/api/catalog/v1/admin/tours/t/images/same-id').flush({}, {status:413,statusText:'Too large'});
    expect(store.selectSnapshot(ImagesState.view).error).toContain('2 MiB');
    expect(store.selectSnapshot(ImagesState.view).saving).toBe(false);
    store.dispatch(new RecoverImage('t','i')).subscribe({error:()=>{}});
    http.expectOne('/api/catalog/v1/admin/tours/t/images/i/recover').flush({}, {status:503,statusText:'Unavailable'});
    expect(store.selectSnapshot(ImagesState.view).error).toContain('durable upload');
  });
  it('rejects unsupported local files and serializes mutations',()=>{
    load();
    store.dispatch(new UploadImage('t','x',new File(['svg'],'x.svg',{type:'image/svg+xml'}))).subscribe({error:()=>{}});
    http.expectNone('/api/catalog/v1/admin/tours/t/images/x');
    store.dispatch(new RemoveImage('t','i')).subscribe();const pending=http.expectOne('/api/catalog/v1/admin/tours/t/images/i');
    let denied=false;store.dispatch(new RemoveImage('t','i')).subscribe({error:()=>denied=true});expect(denied).toBe(true);
    store.dispatch(new UserSessionChanged()).subscribe();pending.flush(null);
    http.expectOne('/api/catalog/v1/tours/t/images').flush([image]);
    expect(store.selectSnapshot(ImagesState.view).items).toEqual([]);
  });
  it('does not apply a mutation result after navigation to a different tour',()=>{
    load();store.dispatch(new RemoveImage('t','i')).subscribe();const pending=http.expectOne('/api/catalog/v1/admin/tours/t/images/i');
    store.dispatch(new LoadImages('next')).subscribe();http.expectOne('/api/catalog/v1/tours/next/images').flush([]);
    pending.flush(null);http.expectOne('/api/catalog/v1/tours/t/images').flush([image]);
    expect(store.selectSnapshot(ImagesState.view).tour).toBe('next');expect(store.selectSnapshot(ImagesState.view).items).toEqual([]);
    expect(store.selectSnapshot(ImagesState.view).saving).toBe(false);
  });
  it('renders authorized proxy images and a placeholder after image download failure',async()=>{
    const fixture=TestBed.createComponent(TourImagesPanel);fixture.componentRef.setInput('tour','t');fixture.detectChanges();
    http.expectOne('/api/catalog/v1/tours/t/images').flush([image]);fixture.detectChanges();
    const element:HTMLImageElement=fixture.nativeElement.querySelector('img');
    expect(element.getAttribute('src')).toBe(image.url);
    element.dispatchEvent(new Event('error'));fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Image unavailable');
    expect(fixture.nativeElement.querySelector('input[type=file]')).toBeNull();
    fixture.destroy();
  });
});
