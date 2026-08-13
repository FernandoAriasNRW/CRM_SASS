import { Component, inject, input, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideLoader2, lucideCircleAlert } from '@ng-icons/lucide';
import {
  CustomFieldsService, SEPARADOR_MULTIPLE, type CustomFieldValue,
} from '../../core/custom-fields.service';
import { UsersService } from '../../core/users.service';
import { SkeletonComponent } from './skeleton.component';

/**
 * Pinta los campos personalizados de una entidad y guarda cada uno al cambiarlo.
 *
 * Se guarda campo a campo y no con un botón «guardar todo»: el resto del detalle de tarea
 * funciona así, y mezclar los dos modos en la misma pantalla haría dudar de si lo escrito
 * quedó guardado.
 *
 * **Los errores del servidor se muestran junto al campo**, no como un aviso genérico. El
 * backend valida y explica por qué —«no es un número», «no está entre las opciones»— y esa
 * frase sólo sirve si se lee al lado de la casilla que la provocó. Y el valor se revierte:
 * dejar en pantalla lo que el servidor rechazó es la clase de mentira que este proyecto ya
 * corrigió en tableros y prioridad.
 */
@Component({
  selector: 'app-custom-fields-form',
  standalone: true,
  imports: [FormsModule, NgIconComponent, SkeletonComponent],
  viewProviders: [provideIcons({ lucideLoader2, lucideCircleAlert })],
  templateUrl: './custom-fields-form.component.html',
})
export class CustomFieldsFormComponent implements OnInit {
  /** «Tarea» o «Proyecto». */
  readonly entidad = input.required<string>();
  readonly entityId = input.required<string>();

  private readonly servicio = inject(CustomFieldsService);
  private readonly usuarios = inject(UsersService);

  readonly campos = signal<CustomFieldValue[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal<string | null>(null);
  readonly errores = signal<Record<string, string>>({});

  /** Copia de lo último guardado, para poder revertir si el servidor rechaza. */
  private ultimoValido: Record<string, string | null> = {};

  ngOnInit(): void {
    this.cargar();
    if (!this.usuarios.users().length) this.usuarios.loadTenantUsers().subscribe();
  }

  get personas() { return this.usuarios.users(); }

  cargar(): void {
    this.cargando.set(true);
    this.servicio.valoresDe(this.entidad(), this.entityId()).subscribe({
      next: campos => {
        this.campos.set(campos ?? []);
        this.ultimoValido = Object.fromEntries((campos ?? []).map(c => [c.definitionId, c.valor]));
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false),
    });
  }

  /** Lo marcado en una selección múltiple. */
  estaMarcada(campo: CustomFieldValue, opcion: string): boolean {
    return (campo.valor ?? '').split(SEPARADOR_MULTIPLE).includes(opcion);
  }

  alternarOpcion(campo: CustomFieldValue, opcion: string): void {
    const actuales = (campo.valor ?? '').split(SEPARADOR_MULTIPLE).filter(Boolean);
    const nuevas = actuales.includes(opcion)
      ? actuales.filter(o => o !== opcion)
      : [...actuales, opcion];

    this.guardar(campo, nuevas.join(SEPARADOR_MULTIPLE));
  }

  /**
   * Guarda un valor y deja el error del servidor junto al campo si lo rechaza.
   */
  guardar(campo: CustomFieldValue, valor: string | null): void {
    const anterior = this.ultimoValido[campo.definitionId] ?? null;
    const limpio = valor === '' ? null : valor;

    this.aplicarEnPantalla(campo.definitionId, limpio);
    this.guardando.set(campo.definitionId);
    this.limpiarError(campo.definitionId);

    this.servicio.guardarValor(campo.definitionId, this.entityId(), limpio).subscribe({
      next: () => {
        this.ultimoValido[campo.definitionId] = limpio;
        this.guardando.set(null);
      },
      error: respuesta => {
        this.aplicarEnPantalla(campo.definitionId, anterior);
        this.guardando.set(null);
        this.errores.update(actuales => ({
          ...actuales,
          [campo.definitionId]: this.mensajeDelServidor(respuesta),
        }));
      },
    });
  }

  private aplicarEnPantalla(definitionId: string, valor: string | null): void {
    this.campos.update(campos => campos.map(c => c.definitionId === definitionId ? { ...c, valor } : c));
  }

  private limpiarError(definitionId: string): void {
    this.errores.update(actuales => {
      const copia = { ...actuales };
      delete copia[definitionId];
      return copia;
    });
  }

  private mensajeDelServidor(respuesta: unknown): string {
    const cuerpo = (respuesta as { error?: unknown })?.error;

    if (typeof cuerpo === 'string' && cuerpo.trim()) return cuerpo;

    // El manejador global devuelve ProblemDetails; su `detail` es el mensaje del dominio.
    const detalle = (cuerpo as { detail?: string } | undefined)?.detail;
    return detalle?.trim() ? detalle : $localize`No se pudo guardar el valor`;
  }

  nombreDeUsuario(id: string | null): string {
    if (!id) return '';
    return this.usuarios.getUser(id)?.name ?? `${id.slice(0, 8)}…`;
  }
}
