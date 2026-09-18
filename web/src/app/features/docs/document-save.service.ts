import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime } from 'rxjs';
import { ToastService } from '../../shared/services/toast.service';
import { DocsService } from './docs.service';

/** Un cambio de página por mandar: el contenido entero, no un diferencial. */
export interface PageChange {
  pageId: string;
  title: string;
  content: string;
}

export type SaveState = 'idle' | 'pending' | 'saving' | 'saved' | 'error';

/**
 * El guardado automático del editor de documentos.
 *
 * Estaba dentro de `DocsComponent`, junto al editor, el árbol, la exportación y los comentarios:
 * 1265 líneas donde un cambio en cómo se guarda obligaba a leer cómo se pinta el árbol. Aquí queda
 * sólo lo que decide **cuándo** se manda algo y **qué se cuenta** de ello en la cabecera.
 *
 * Se registra en el componente (`providers`) y no en la raíz: el estado de guardado es de un
 * editor abierto, y compartirlo haría que abrir Documentos en otra ruta heredara el «guardado a
 * las 10:42» de la anterior.
 */
@Injectable()
export class DocumentSaveService {
  private readonly docsService = inject(DocsService);
  private readonly toast = inject(ToastService);

  /**
   * En qué punto está el guardado.
   *
   * <b>Antes la cabecera decía «Saved just now» y era una cadena escrita a mano</b>, pintada
   * siempre, sin relación con lo que contestara el servidor. Y el guardado se suscribía sin
   * manejador de error: si la petición fallaba —red, sesión caducada, error del servidor— no
   * ocurría nada. Se podía escribir media hora leyendo «guardado» y perderlo entero al recargar.
   */
  readonly state = signal<SaveState>('idle');

  /** Cuándo se guardó por última vez de verdad, para poder decir la hora en vez de «ahora». */
  readonly savedAt = signal<Date | null>(null);

  private readonly pageChanges$ = new Subject<PageChange>();

  /** El título del documento se guarda aparte, y con su propio respiro entre teclas. */
  private readonly documentTitles$ = new Subject<{ documentId: string; title: string }>();

  /** Lo último que no se pudo guardar, para poder reintentarlo sin perderlo. */
  private pendingRetry: PageChange | null = null;

  constructor() {
    const destroyRef = inject(DestroyRef);

    this.pageChanges$.pipe(debounceTime(1000), takeUntilDestroyed(destroyRef))
      .subscribe(change => this.save(change));

    this.documentTitles$.pipe(debounceTime(700), takeUntilDestroyed(destroyRef))
      .subscribe(({ documentId, title }) => this.saveDocumentTitle(documentId, title));
  }

  /**
   * Apunta un cambio de página para mandarlo cuando se deje de escribir.
   *
   * «Pendiente» se pone aquí, no al guardar: entre la última tecla y la petición pasa un segundo
   * entero, y durante ese segundo la cabecera decía «guardado» aunque hubiera cambios sin mandar.
   */
  queuePage(change: PageChange): void {
    this.state.set('pending');
    this.pageChanges$.next(change);
  }

  queueDocumentTitle(documentId: string, title: string): void {
    this.documentTitles$.next({ documentId, title });
  }

  /** Reintenta lo último que no se pudo guardar. */
  retry(): void {
    if (this.pendingRetry) this.save(this.pendingRetry);
  }

  /**
   * Manda el contenido al servidor y cuenta lo que pasa.
   *
   * El error no se traga: se enseña en la cabecera, se avisa una vez, y lo que no se pudo guardar
   * queda apartado para reintentarlo. Perder el texto de alguien porque caducó una sesión es el
   * peor fallo que puede tener un editor, y era el que tenía.
   */
  private save(change: PageChange): void {
    this.state.set('saving');

    this.docsService.updatePage(change.pageId, { title: change.title, content: change.content }).subscribe({
      next: () => {
        this.pendingRetry = null;
        this.savedAt.set(new Date());
        this.state.set('saved');
      },
      error: (err) => {
        // Se guarda lo que falló, no lo que hay ahora en el editor: si alguien cambia de página
        // tras el fallo, el reintento tiene que mandar el texto que no llegó, no el de la página
        // nueva.
        this.pendingRetry = change;
        this.state.set('error');

        this.toast.error(
          $localize`No se pudo guardar`,
          $localize`Los cambios siguen en pantalla. Vuelve a intentarlo desde la cabecera.`);

        console.error('No se pudo guardar la página', err);
      }
    });
  }

  private saveDocumentTitle(documentId: string, title: string): void {
    const trimmed = title.trim();
    // Un título vacío lo rechaza el servidor. Se deja de mandar en vez de enseñar un error por
    // cada tecla mientras alguien borra el título para escribir otro.
    if (!trimmed) return;

    this.docsService.renameDocument(documentId, { title: trimmed }).subscribe({
      next: () => this.savedAt.set(new Date()),
      error: (err) => {
        this.state.set('error');
        this.toast.error($localize`No se pudo renombrar el documento`);
        console.error('No se pudo renombrar el documento', err);
      }
    });
  }
}
