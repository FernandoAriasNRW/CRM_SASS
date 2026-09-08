import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DiaDesplegadoComponent } from './dia-desplegado.component';
import type { AgendaDeUnDia, CosaDelDia } from './calendario.service';

/**
 * El día desplegado: sus horas, y lo que va arriba porque no tiene hora.
 *
 * Se comprueba sobre todo <b>dónde cae cada cosa</b>. Una tarea que vence no ocurre a una hora
 * concreta, y colocarla en una inventada haría creer que sí; y un día que empieza a las 00:00
 * obliga a desplazarse ocho franjas vacías para ver la primera reunión.
 */
describe('DiaDesplegadoComponent', () => {
  let fixture: ComponentFixture<DiaDesplegadoComponent>;
  let componente: DiaDesplegadoComponent;

  const evento = (id: string, hora: string, anulado = false): CosaDelDia => ({
    tipo: 'Evento', id, titulo: `Evento ${id}`, detalle: null,
    hora, horaFin: null, anulado
  });

  const agenda = (parcial: Partial<AgendaDeUnDia> = {}): AgendaDeUnDia => ({
    dia: '2026-09-08',
    eventos: [],
    tareasQueVencen: [],
    ticketsDelDia: [],
    proyectosQueTerminan: [],
    ...parcial
  });

  const pintar = (a: AgendaDeUnDia) => {
    fixture.componentRef.setInput('agenda', a);
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DiaDesplegadoComponent] }).compileComponents();

    fixture = TestBed.createComponent(DiaDesplegadoComponent);
    componente = fixture.componentInstance;
    pintar(agenda());
  });

  it('un día vacío sigue teniendo horas donde pulsar', () => {
    const franjas = componente.franjas();

    expect(franjas[0].hora).toBe(8);
    expect(franjas[franjas.length - 1].hora).toBe(19);
  });

  /**
   * Con algo antes de las 8 o después de las 19, la franja se estira. Si no, el evento existiría
   * en los datos y no habría dónde pintarlo.
   */
  it('se estira para que quepa lo que cae fuera del horario normal', () => {
    pintar(agenda({ eventos: [evento('a', '2026-09-08T06:30:00'), evento('b', '2026-09-08T22:00:00')] }));

    const horas = componente.franjas().map(f => f.hora);
    expect(horas[0]).toBe(6);
    expect(horas[horas.length - 1]).toBe(22);
  });

  it('coloca cada evento en su hora', () => {
    pintar(agenda({ eventos: [evento('a', '2026-09-08T09:15:00'), evento('b', '2026-09-08T09:45:00')] }));

    const alasNueve = componente.franjas().find(f => f.hora === 9)!;
    expect(alasNueve.eventos.map(e => e.id)).toEqual(['a', 'b']);
  });

  /**
   * Lo que vence ese día va arriba, no repartido por horas. Es la diferencia entre «esto pasa a
   * las 10» y «esto hay que tenerlo hecho hoy».
   */
  it('lo que no tiene hora va aparte', () => {
    const tarea: CosaDelDia = {
      tipo: 'Tarea', id: 't1', titulo: 'Migrar la base', detalle: 'En curso',
      hora: null, horaFin: null, anulado: false
    };

    pintar(agenda({ tareasQueVencen: [tarea], eventos: [evento('a', '2026-09-08T09:00:00')] }));

    expect(componente.sinHora().map(c => c.id)).toEqual(['t1']);
    expect(componente.franjas().flatMap(f => f.eventos).map(e => e.id)).toEqual(['a']);
  });

  it('el resumen cuenta sólo lo que hay', () => {
    expect(componente.resumen()).toBe('Nada en el calendario este día');

    pintar(agenda({ eventos: [evento('a', '2026-09-08T09:00:00')] }));
    expect(componente.resumen()).toBe('1 evento');

    pintar(agenda({ eventos: [evento('a', '2026-09-08T09:00:00'), evento('b', '2026-09-08T10:00:00')] }));
    expect(componente.resumen()).toBe('2 eventos');
  });

  /** Un evento anulado se sigue viendo, tachado: es todo el sentido de separarlo de la papelera. */
  it('pinta tachado lo anulado, sin quitarlo', () => {
    pintar(agenda({ eventos: [evento('a', '2026-09-08T09:00:00', true)] }));

    const boton = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLElement>
    ).find(b => b.textContent?.includes('Evento a'))!;

    expect(boton).withContext('un evento anulado no puede desaparecer del día').toBeTruthy();
    expect(boton.className).toContain('line-through');
  });
});
