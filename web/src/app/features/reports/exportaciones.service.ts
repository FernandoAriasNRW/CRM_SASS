import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import { mensajeDeError } from '../../shared/utils/mensaje-de-error';

/** Los estados que puede tener una exportación. Son los del servidor, sin traducir. */
export type EstadoDeExportacion = 'Pendiente' | 'Generando' | 'Lista' | 'Fallida';

export interface Exportacion {
  id: string;
  reportId: string;
  formato: string;
  estado: EstadoDeExportacion;
  solicitadaUtc: string;
  terminadaUtc: string | null;
  nombreDeFichero: string | null;
  tamanoBytes: number;
  error: string | null;
  intentos: number;
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
export class ExportacionesService {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  /**
   * Cada cuánto se pregunta si ya está.
   *
   * Dos segundos: el trabajador del servidor sondea cada cinco, así que preguntar más a menudo
   * sólo añade peticiones sin adelantar nada, y preguntar menos deja la pantalla quieta después
   * de que el fichero ya esté listo.
   */
  private static readonly CADA = 2000;

  /**
   * Cuánto se espera antes de dejar de mirar.
   *
   * Rendirse es distinto de fallar, y se dice distinto: pasado este tiempo la exportación **sigue
   * en su lista** y se puede descargar cuando termine. Lo que se acaba es la espera, no el
   * trabajo.
   */
  private static readonly HASTA = 120_000;

  /** Las exportaciones que esta pantalla está vigilando, por identificador de informe. */
  readonly enMarcha = signal<Record<string, EstadoDeExportacion>>({});

  /**
   * Pide la exportación y, cuando esté, la descarga.
   *
   * Devuelve enseguida el control a quien pulsó: lo que tarda ocurre después, con la pantalla
   * usable.
   */
  async exportar(reportId: string, formato: string): Promise<void> {
    try {
      const trabajo = await firstValueFrom(
        this.api.post<Exportacion>(`/reports/${reportId}/exportar?format=${formato}`, {}));

      this.marcar(reportId, trabajo.estado);
      this.toast.info($localize`Preparando el informe. Te avisamos cuando esté.`);

      const final = await this.esperar(trabajo.id);

      if (final === null) {
        // Ni lista ni fallida: se acabó la paciencia. Se dice tal cual, porque el trabajo sigue.
        this.toast.info($localize`El informe está tardando. Seguirá generándose; búscalo en la lista de exportaciones.`);
        return;
      }

      if (final.estado === 'Fallida') {
        // El motivo viene del servidor y se enseña entero. «Falló» a secas obliga a preguntar.
        this.toast.error(final.error ?? $localize`La exportación falló.`);
        this.marcar(reportId, 'Fallida');
        return;
      }

      this.marcar(reportId, 'Lista');
      await this.descargar(final);
    } catch (error) {
      this.toast.error(mensajeDeError(error, $localize`No se pudo exportar el informe.`));
      this.olvidar(reportId);
    }
  }

  /** Las exportaciones de un informe, para pintarlas. */
  exportacionesDe(reportId: string) {
    return this.api.get<Exportacion[]>(`/reports/${reportId}/exportaciones`);
  }

  /**
   * Pregunta cada dos segundos hasta que termine.
   *
   * Devuelve `null` si se acaba la paciencia, que **no** es lo mismo que fallar: hay que poder
   * decir «sigue en marcha» sin dar a entender que se perdió.
   */
  private async esperar(exportacionId: string): Promise<Exportacion | null> {
    const limite = Date.now() + ExportacionesService.HASTA;

    while (Date.now() < limite) {
      const estado = await firstValueFrom(this.api.get<Exportacion>(`/exportaciones/${exportacionId}`));

      if (estado.estado === 'Lista' || estado.estado === 'Fallida') return estado;

      await new Promise(seguir => setTimeout(seguir, ExportacionesService.CADA));
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
  private async descargar(exportacion: Exportacion): Promise<void> {
    const respuesta = await firstValueFrom(
      this.api.descargarFichero(`/exportaciones/${exportacion.id}/descargar`));

    const cuerpo = respuesta.body;
    if (!cuerpo) {
      this.toast.error($localize`La descarga llegó vacía.`);
      return;
    }

    const nombre = this.nombreDe(respuesta.headers.get('content-disposition'))
      ?? exportacion.nombreDeFichero
      ?? `informe.${exportacion.formato.toLowerCase()}`;

    const url = URL.createObjectURL(cuerpo);

    try {
      const enlace = document.createElement('a');
      enlace.href = url;
      enlace.download = nombre;
      enlace.click();
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
  private nombreDe(cabecera: string | null): string | null {
    if (!cabecera) return null;

    const conAcentos = /filename\*=UTF-8''([^;]+)/i.exec(cabecera);
    if (conAcentos) return decodeURIComponent(conAcentos[1]);

    const simple = /filename="?([^";]+)"?/i.exec(cabecera);
    return simple ? simple[1] : null;
  }

  private marcar(reportId: string, estado: EstadoDeExportacion): void {
    this.enMarcha.update(actual => ({ ...actual, [reportId]: estado }));
  }

  private olvidar(reportId: string): void {
    this.enMarcha.update(actual => {
      const copia = { ...actual };
      delete copia[reportId];
      return copia;
    });
  }
}
