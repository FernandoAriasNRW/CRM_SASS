import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import { errorMessage } from '../../shared/utils/error-message';

/** Los estados que puede tener una exportación. Son los del servidor, sin traducir. */
export type ExportStatus = 'Pending' | 'Generating' | 'Ready' | 'Failed';

export interface Export {
  id: string;
  reportId: string;
  format: string;
  status: ExportStatus;
  requestedAtUtc: string;
  finishedAtUtc: string | null;
  fileName: string | null;
  sizeBytes: number;
  error: string | null;
  attempts: number;
}

/**
 * Pide exportaciones, las vigila hasta que terminan y descarga el fichero.
 *
 * **El fichero lo hace el servidor, no el navegador.** Es la advertencia del plan: exportar en
 * el cliente es rápido de escribir, se rompe con volumen —el navegador no puede con cien mil
 * filas— y además no sirve para los informes programados, que ocurren sin nadie delante.
 *
 * Aquí sólo se pide, se espera y se guarda.
 */
@Injectable({ providedIn: 'root' })
export class ExportsService {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  /**
   * Cada cuánto se pregunta si ya está.
   *
   * Dos segundos: el trabajador del servidor sondea cada cinco, así que preguntar más a menudo
   * sólo añade peticiones sin adelantar nada, y preguntar menos deja la pantalla quieta después
   * de que el fichero ya esté listo.
   */
  private static readonly POLL_INTERVAL_MS = 2000;

  /**
   * Cuánto se espera antes de dejar de mirar.
   *
   * Rendirse es distinto de fallar, y se dice distinto: pasado este tiempo la exportación **sigue
   * en su lista** y se puede descargar cuando termine. Lo que se acaba es la espera, no el
   * trabajo.
   */
  private static readonly MAX_WAIT_MS = 120_000;

  /** Las exportaciones que esta pantalla está vigilando, por identificador de informe. */
  readonly inProgress = signal<Record<string, ExportStatus>>({});

  /**
   * Pide la exportación y, cuando esté, la descarga.
   *
   * Devuelve enseguida el control a quien pulsó: lo que tarda ocurre después, con la pantalla
   * usable.
   */
  async requestExport(reportId: string, format: string): Promise<void> {
    try {
      const job = await firstValueFrom(
        this.api.post<Export>(`/reports/${reportId}/export?format=${format}`, {}));

      this.mark(reportId, job.status);
      this.toast.info($localize`Preparando el informe. Te avisamos cuando esté.`);

      const final = await this.waitFor(job.id);

      if (final === null) {
        // Ni lista ni fallida: se acabó la paciencia. Se dice tal cual, porque el trabajo sigue.
        this.toast.info($localize`El informe está tardando. Seguirá generándose; búscalo en la lista de exportaciones.`);
        return;
      }

      if (final.status === 'Failed') {
        // El motivo viene del servidor y se enseña entero. «Falló» a secas obliga a preguntar.
        this.toast.error(final.error ?? $localize`La exportación falló.`);
        this.mark(reportId, 'Failed');
        return;
      }

      this.mark(reportId, 'Ready');
      await this.download(final);
    } catch (error) {
      this.toast.error(errorMessage(error, $localize`No se pudo exportar el informe.`));
      this.forget(reportId);
    }
  }

  /** Las exportaciones de un informe, para pintarlas. */
  exportsOf(reportId: string) {
    return this.api.get<Export[]>(`/reports/${reportId}/exports`);
  }

  /**
   * Pregunta cada dos segundos hasta que termine.
   *
   * Devuelve `null` si se acaba la paciencia, que **no** es lo mismo que fallar: hay que poder
   * decir «sigue en marcha» sin dar a entender que se perdió.
   */
  private async waitFor(exportId: string): Promise<Export | null> {
    const deadline = Date.now() + ExportsService.MAX_WAIT_MS;

    while (Date.now() < deadline) {
      const latest = await firstValueFrom(this.api.get<Export>(`/exports/${exportId}`));

      if (latest.status === 'Ready' || latest.status === 'Failed') return latest;

      await new Promise(resolve => setTimeout(resolve, ExportsService.POLL_INTERVAL_MS));
    }

    return null;
  }

  /**
   * Guarda el fichero.
   *
   * El nombre sale de la cabecera `Content-Disposition` y, si no viniera, del que dice la propia
   * exportación. Sin ninguno de los dos, el navegador guardaría el fichero con el identificador
   * por nombre y quien lo descargue acabaría con «a3f2…» en la carpeta de descargas.
   */
  private async download(job: Export): Promise<void> {
    const response = await firstValueFrom(
      this.api.downloadFile(`/exports/${job.id}/download`));

    const body = response.body;
    if (!body) {
      this.toast.error($localize`La descarga llegó vacía.`);
      return;
    }

    const name = this.fileNameFrom(response.headers.get('content-disposition'))
      ?? job.fileName
      ?? `informe.${job.format.toLowerCase()}`;

    const url = URL.createObjectURL(body);

    try {
      const link = document.createElement('a');
      link.href = url;
      link.download = name;
      link.click();
    } finally {
      // Se libera siempre. Sin esto, cada descarga deja el fichero entero retenido en memoria
      // mientras la pestaña siga abierta, y quien exporte diez informes grandes lo nota.
      URL.revokeObjectURL(url);
    }

    this.toast.success($localize`Informe descargado.`);
  }

  /**
   * Saca el nombre de la cabecera.
   *
   * Se mira primero `filename*`, que es la forma que admite acentos (RFC 5987): un informe
   * llamado «Diseño» viaja ahí correctamente y en `filename` a secas llegaría destrozado.
   */
  private fileNameFrom(header: string | null): string | null {
    if (!header) return null;

    const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header);
    if (encoded) return decodeURIComponent(encoded[1]);

    const simple = /filename="?([^";]+)"?/i.exec(header);
    return simple ? simple[1] : null;
  }

  private mark(reportId: string, status: ExportStatus): void {
    this.inProgress.update(current => ({ ...current, [reportId]: status }));
  }

  private forget(reportId: string): void {
    this.inProgress.update(current => {
      const copy = { ...current };
      delete copy[reportId];
      return copy;
    });
  }
}
