import type { DocumentDto } from './docs.service';

/**
 * Una plantilla que se puede usar, venga del sistema o del equipo.
 *
 * Las dos clases se pintaban por separado —una cuadrícula de tarjetas de colores para las del
 * sistema y otra lista aparte para «Mis plantillas»— y eso obligaba a elegir dónde mirar antes de
 * elegir plantilla. Aquí son lo mismo: lo único que cambia es de dónde sale el contenido.
 *
 * La `clave` es el identificador con el que se pide la creación **y** con el que el servidor
 * cuenta los usos: la clave del sistema para las predefinidas, el identificador del documento
 * para las propias.
 */
export interface PlantillaDisponible {
  clave: string;
  titulo: string;
  descripcion: string;
  icono: string;
  /** Clases del recuadro del icono. */
  iconoFondo: string;
  /** Clases del borde de la tarjeta. */
  borde: string;
  /** Clases del degradado de fondo. */
  degradado: string;
  esPropia: boolean;
  /** Veces que se ha creado un documento desde ella en este inquilino. */
  veces: number;
}

/**
 * Las plantillas que trae el producto.
 *
 * Los títulos y las descripciones estaban en inglés y sin marcar, así que se veían en inglés
 * también con la aplicación en español. El contenido que genera cada una sigue estando en el
 * servidor —ahí es donde hay que traducirlo—; esto es sólo cómo se anuncian.
 */
export const PLANTILLAS_DEL_SISTEMA: readonly PlantillaDisponible[] = [
  {
    clave: 'project-overview',
    titulo: $localize`Resumen de proyecto`,
    descripcion: $localize`Objetivos, alcance e hitos`,
    icono: 'lucideFileText',
    iconoFondo: 'bg-warning text-white shadow-amber-500/30',
    borde: 'border-warning dark:border-warning/50 hover:border-warning dark:hover:border-warning',
    degradado: 'from-amber-500/10 via-orange-500/5 to-transparent dark:from-amber-500/20 dark:via-orange-500/10',
    esPropia: false,
    veces: 0
  },
  {
    clave: 'meeting-notes',
    titulo: $localize`Acta de reunión`,
    descripcion: $localize`Orden del día, notas y acuerdos`,
    icono: 'lucideCalendar',
    iconoFondo: 'bg-warning text-white shadow-yellow-500/30',
    borde: 'border-warning dark:border-warning/50 hover:border-warning dark:hover:border-warning',
    degradado: 'from-yellow-500/10 via-amber-500/5 to-transparent dark:from-yellow-500/20 dark:via-amber-500/10',
    esPropia: false,
    veces: 0
  },
  {
    clave: 'wiki',
    titulo: $localize`Wiki`,
    descripcion: $localize`Toda la información en un sitio`,
    icono: 'lucideBookOpen',
    iconoFondo: 'bg-primary text-white shadow-blue-600/30',
    borde: 'border-primary dark:border-primary/50 hover:border-primary dark:hover:border-primary',
    degradado: 'from-blue-500/10 via-indigo-500/5 to-transparent dark:from-blue-500/20 dark:via-indigo-500/10',
    esPropia: false,
    veces: 0
  },
  {
    clave: 'client-onboarding',
    titulo: $localize`Alta de cliente`,
    descripcion: $localize`Ficha, requisitos y traspaso`,
    icono: 'lucideBriefcase',
    iconoFondo: 'bg-primary text-white shadow-purple-600/30',
    borde: 'border-primary dark:border-primary/50 hover:border-primary dark:hover:border-primary',
    degradado: 'from-purple-500/10 via-pink-500/5 to-transparent dark:from-purple-500/20 dark:via-pink-500/10',
    esPropia: false,
    veces: 0
  }
];

/** Lo que se enseña sin desplegar nada. */
export const PLANTILLAS_A_LA_VISTA = 4;

const DESCRIPCION_POR_DEFECTO = $localize`Plantilla guardada por el equipo`;

/**
 * Junta las del sistema con las del equipo y las ordena por uso.
 *
 * <b>El orden lo decide el contador del servidor, no el cliente.</b> Guardarlo en el navegador
 * habría sido más barato, pero entonces cada persona vería un orden distinto y quien entrara
 * nuevo vería cuatro plantillas al azar; el contador es del inquilino, así que la galería llega
 * ya ordenada por lo que el equipo usa.
 *
 * A igualdad de usos —y al principio todas empatan a cero— manda el orden de declaración: las del
 * sistema primero y las propias después, de la más reciente a la más antigua. Sin ese desempate
 * las tarjetas bailarían de sitio entre recargas.
 */
export function plantillasDisponibles(
  propias: readonly DocumentDto[],
  usos: ReadonlyMap<string, number>
): PlantillaDisponible[] {
  const delSistema = PLANTILLAS_DEL_SISTEMA.map(p => ({ ...p, veces: usos.get(p.clave) ?? 0 }));

  const delEquipo: PlantillaDisponible[] = [...propias]
    .sort((a, b) => (b.updatedAtUtc ?? '').localeCompare(a.updatedAtUtc ?? ''))
    .map(doc => ({
      clave: doc.id,
      titulo: doc.title,
      descripcion: doc.description || DESCRIPCION_POR_DEFECTO,
      icono: 'lucideLayoutTemplate',
      iconoFondo: 'bg-primary text-primary-foreground',
      borde: 'border-primary dark:border-primary/40 hover:border-primary',
      degradado: 'from-primary/10 via-primary/5 to-transparent dark:from-primary/20',
      esPropia: true,
      veces: usos.get(doc.id) ?? 0
    }));

  const todas = [...delSistema, ...delEquipo];

  // `sort` es estable en todos los navegadores que soporta Angular, así que el orden de entrada
  // hace de desempate sin necesidad de añadirlo a la comparación.
  return todas.sort((a, b) => b.veces - a.veces);
}
