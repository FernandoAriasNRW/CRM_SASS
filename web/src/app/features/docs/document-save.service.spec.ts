import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { ToastService } from '../../shared/services/toast.service';
import { DocsService } from './docs.service';
import { DocumentSaveService } from './document-save.service';

/**
 * El guardado automático, ya fuera del componente del editor.
 *
 * Lo que se fija aquí es lo que costó caro cuando estaba mal: que la cabecera no diga «guardado»
 * mientras hay cambios sin mandar, que un fallo no se trague, y que el reintento mande **lo que
 * falló** y no lo que haya en pantalla después.
 */
describe('DocumentSaveService', () => {
  let service: DocumentSaveService;
  let docs: jasmine.SpyObj<DocsService>;
  let toast: jasmine.SpyObj<ToastService>;

  const change = (content: string) => ({ pageId: 'p1', title: 'Acta', content });

  beforeEach(() => {
    docs = jasmine.createSpyObj<DocsService>('DocsService', ['updatePage', 'renameDocument']);
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['error']);

    TestBed.configureTestingModule({
      providers: [
        DocumentSaveService,
        { provide: DocsService, useValue: docs },
        { provide: ToastService, useValue: toast }
      ]
    });

    service = TestBed.inject(DocumentSaveService);
  });

  it('marca pendiente al instante y sólo manda al dejar de escribir', fakeAsync(() => {
    docs.updatePage.and.returnValue(of(void 0));

    service.queuePage(change('a'));
    service.queuePage(change('ab'));

    expect(service.state()).toBe('pending');
    expect(docs.updatePage).not.toHaveBeenCalled();

    tick(1000);

    expect(docs.updatePage).toHaveBeenCalledOnceWith('p1', { title: 'Acta', content: 'ab' });
    expect(service.state()).toBe('saved');
    expect(service.savedAt()).not.toBeNull();
  }));

  it('un fallo se cuenta y el reintento manda lo que no llegó', fakeAsync(() => {
    docs.updatePage.and.returnValue(throwError(() => new Error('sesión caducada')));

    service.queuePage(change('lo que falló'));
    tick(1000);

    expect(service.state()).toBe('error');
    expect(toast.error).toHaveBeenCalledTimes(1);

    docs.updatePage.and.returnValue(of(void 0));
    service.retry();

    expect(docs.updatePage).toHaveBeenCalledWith('p1', { title: 'Acta', content: 'lo que falló' });
    expect(service.state()).toBe('saved');
  }));

  it('no manda un título de documento vacío', fakeAsync(() => {
    service.queueDocumentTitle('d1', '   ');
    tick(700);

    expect(docs.renameDocument).not.toHaveBeenCalled();
  }));

  it('manda el título recortado tras el respiro', fakeAsync(() => {
    docs.renameDocument.and.returnValue(of(void 0));

    service.queueDocumentTitle('d1', '  Plan  ');
    tick(700);

    expect(docs.renameDocument).toHaveBeenCalledOnceWith('d1', { title: 'Plan' });
  }));
});
