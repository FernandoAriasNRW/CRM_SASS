import { Injectable, inject, signal } from '@angular/core';
import { ToastService } from '../../shared/services/toast.service';
import { DocsService, DocumentDto } from './docs.service';

/**
 * Exportar un documento a PDF o a HTML.
 *
 * Vivía dentro de `DocsComponent`. Se separa porque no tiene nada que ver con editar: carga una
 * librería aparte, habla con otro endpoint y fabrica ficheros, y mezclado con el editor era una
 * razón más para tener que leer 1265 líneas antes de tocar cualquiera de las dos cosas.
 *
 * Se registra en el componente, como el guardado: «exportando» es del documento abierto.
 */
@Injectable()
export class DocumentExportService {
  private readonly docsService = inject(DocsService);
  private readonly toast = inject(ToastService);

  /** Para desactivar los botones de exportar mientras se genera el fichero. */
  readonly exporting = signal(false);

  /**
   * Guarda en PDF lo que se ve en el editor.
   *
   * `html2pdf.js` estaba en las dependencias y **no se importaba en ningún sitio**, así que
   * `window.html2pdf` era siempre `undefined` y el botón caía al `window.print()` de reserva, que
   * imprime la aplicación entera con su barra lateral en vez del documento.
   *
   * Se carga en el momento y no arriba del fichero: son unos 700 kB que sólo hacen falta si
   * alguien pulsa el botón, y cargarlos siempre los mete en el paquete de Documentos.
   */
  async exportPdf(content: HTMLElement, title: string | undefined): Promise<void> {
    this.exporting.set(true);
    try {
      const { default: html2pdf } = await import('html2pdf.js');

      await html2pdf()
        .set({ margin: 10, filename: `${title || 'documento'}.pdf` })
        .from(content)
        .save();
    } catch (err) {
      this.toast.error($localize`No se pudo generar el PDF`);
      console.error('No se pudo generar el PDF', err);
    } finally {
      this.exporting.set(false);
    }
  }

  /**
   * Descarga el documento en HTML.
   *
   * Antes hacía `window.open` de la URL de exportación. Una pestaña nueva no lleva la cabecera de
   * sesión y el endpoint la exige: **el botón devolvía 401 siempre**, y como se abría en otra
   * pestaña, ni siquiera se veía el error.
   */
  exportHtml(doc: DocumentDto): void {
    this.exporting.set(true);
    this.docsService.exportHtml(doc.id).subscribe({
      next: (response) => {
        this.exporting.set(false);

        const body = response.body;
        if (!body) {
          this.toast.error($localize`La descarga llegó vacía.`);
          return;
        }

        const url = URL.createObjectURL(body);
        try {
          const link = document.createElement('a');
          link.href = url;
          link.download = `${doc.title || 'documento'}.html`;
          link.click();
        } finally {
          // Sin esto, cada descarga deja el fichero entero retenido en memoria mientras la
          // pestaña siga abierta.
          URL.revokeObjectURL(url);
        }
      },
      error: (err) => {
        this.exporting.set(false);
        this.toast.error($localize`No se pudo exportar el documento`);
        console.error('No se pudo exportar el documento', err);
      }
    });
  }
}
