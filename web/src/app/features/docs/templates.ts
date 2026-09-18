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
export interface AvailableTemplate {
  key: string;
  title: string;
  description: string;
  icon: string;
  /** Clases del recuadro del icono. */
  iconBackground: string;
  /** Clases del borde de la tarjeta. */
  border: string;
  /** Clases del degradado de fondo. */
  gradient: string;
  isCustom: boolean;
  /** Veces que se ha creado un documento desde ella en este inquilino. */
  count: number;
}

/**
 * Las plantillas que trae el producto.
 *
 * Los títulos y las descripciones estaban en inglés y sin marcar, así que se veían en inglés
 * también con la aplicación en español. El contenido que genera cada una sigue estando en el
 * servidor —ahí es donde hay que traducirlo—; esto es sólo cómo se anuncian.
 */
export const BUILT_IN_TEMPLATES: readonly AvailableTemplate[] = [
  {
    key: 'project-overview',
    title: $localize`Resumen de proyecto`,
    description: $localize`Objetivos, alcance e hitos`,
    icon: 'lucideFileText',
    iconBackground: 'bg-warning text-white shadow-amber-500/30',
    border: 'border-warning dark:border-warning/50 hover:border-warning dark:hover:border-warning',
    gradient: 'from-amber-500/10 via-orange-500/5 to-transparent dark:from-amber-500/20 dark:via-orange-500/10',
    isCustom: false,
    count: 0
  },
  {
    key: 'meeting-notes',
    title: $localize`Acta de reunión`,
    description: $localize`Orden del día, notas y acuerdos`,
    icon: 'lucideCalendar',
    iconBackground: 'bg-warning text-white shadow-yellow-500/30',
    border: 'border-warning dark:border-warning/50 hover:border-warning dark:hover:border-warning',
    gradient: 'from-yellow-500/10 via-amber-500/5 to-transparent dark:from-yellow-500/20 dark:via-amber-500/10',
    isCustom: false,
    count: 0
  },
  {
    key: 'wiki',
    title: $localize`Wiki`,
    description: $localize`Toda la información en un sitio`,
    icon: 'lucideBookOpen',
    iconBackground: 'bg-primary text-white shadow-blue-600/30',
    border: 'border-primary dark:border-primary/50 hover:border-primary dark:hover:border-primary',
    gradient: 'from-blue-500/10 via-indigo-500/5 to-transparent dark:from-blue-500/20 dark:via-indigo-500/10',
    isCustom: false,
    count: 0
  },
  {
    key: 'client-onboarding',
    title: $localize`Alta de cliente`,
    description: $localize`Ficha, requisitos y traspaso`,
    icon: 'lucideBriefcase',
    iconBackground: 'bg-primary text-white shadow-purple-600/30',
    border: 'border-primary dark:border-primary/50 hover:border-primary dark:hover:border-primary',
    gradient: 'from-purple-500/10 via-pink-500/5 to-transparent dark:from-purple-500/20 dark:via-pink-500/10',
    isCustom: false,
    count: 0
  }
];

/** Lo que se enseña sin desplegar nada. */
export const VISIBLE_TEMPLATES = 4;

const DEFAULT_DESCRIPTION = $localize`Plantilla guardada por el equipo`;

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
export function availableTemplates(
  custom: readonly DocumentDto[],
  usages: ReadonlyMap<string, number>
): AvailableTemplate[] {
  const builtInTemplates = BUILT_IN_TEMPLATES.map(p => ({ ...p, count: usages.get(p.key) ?? 0 }));

  const teamTemplates: AvailableTemplate[] = [...custom]
    .sort((a, b) => (b.updatedAtUtc ?? '').localeCompare(a.updatedAtUtc ?? ''))
    .map(doc => ({
      key: doc.id,
      title: doc.title,
      description: doc.description || DEFAULT_DESCRIPTION,
      icon: 'lucideLayoutTemplate',
      iconBackground: 'bg-primary text-primary-foreground',
      border: 'border-primary dark:border-primary/40 hover:border-primary',
      gradient: 'from-primary/10 via-primary/5 to-transparent dark:from-primary/20',
      isCustom: true,
      count: usages.get(doc.id) ?? 0
    }));

  const all = [...builtInTemplates, ...teamTemplates];

  // `sort` es estable en todos los navegadores que soporta Angular, así que el orden de entrada
  // hace de desempate sin necesidad de añadirlo a la comparación.
  return all.sort((a, b) => b.count - a.count);
}
