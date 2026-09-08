import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MenuContextualComponent, type OpcionDelMenu } from './menu-contextual.component';

describe('MenuContextualComponent', () => {
  let fixture: ComponentFixture<MenuContextualComponent>;
  let componente: MenuContextualComponent;

  const OPCIONES: OpcionDelMenu[] = [
    { clave: 'crear', etiqueta: 'Crear nuevo evento' },
    { clave: 'ver', etiqueta: 'Ver eventos' },
    { clave: 'papelera', etiqueta: 'Enviar a la papelera', destructiva: true, separadorAntes: true },
    { clave: 'bloqueada', etiqueta: 'No disponible', deshabilitada: true }
  ];

  const raton = (x: number, y: number) =>
    new MouseEvent('contextmenu', { clientX: x, clientY: y, cancelable: true });

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MenuContextualComponent] }).compileComponents();

    fixture = TestBed.createComponent(MenuContextualComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('opciones', OPCIONES);
    fixture.detectChanges();
  });

  it('empieza cerrado y se abre donde se pulsó', () => {
    expect(componente.abierto()).toBeFalse();

    componente.abrirEn(raton(100, 120));
    fixture.detectChanges();

    expect(componente.abierto()).toBeTrue();
    expect(componente.posicion()).toEqual({ x: 100, y: 120 });
  });

  /** Sin esto, el menú del calendario abriría además el día de la celda sobre la que se pulsó. */
  it('cancela el menú del navegador', () => {
    const evento = raton(10, 10);
    componente.abrirEn(evento);

    expect(evento.defaultPrevented).toBeTrue();
  });

  /**
   * El fallo que esto evita: pulsando en la última fila del mes, el menú se salía por abajo y sus
   * últimas opciones —las de borrar— quedaban fuera de la pantalla, inalcanzables.
   */
  it('se recoloca para no salirse de la ventana', () => {
    componente.abrirEn(raton(window.innerWidth - 5, window.innerHeight - 5));

    const { x, y } = componente.posicion();
    expect(x).toBeLessThan(window.innerWidth - 5);
    expect(y).toBeLessThan(window.innerHeight - 5);
  });

  it('avisa de la opción elegida y se cierra', () => {
    const elegidas: string[] = [];
    componente.elegida.subscribe(c => elegidas.push(c));

    componente.abrirEn(raton(10, 10));
    componente.elegir(OPCIONES[0]);

    expect(elegidas).toEqual(['crear']);
    expect(componente.abierto()).toBeFalse();
  });

  it('una opción deshabilitada no hace nada', () => {
    const elegidas: string[] = [];
    componente.elegida.subscribe(c => elegidas.push(c));

    componente.abrirEn(raton(10, 10));
    componente.elegir(OPCIONES[3]);

    expect(elegidas).toEqual([]);
    expect(componente.abierto()).withContext('el menú sigue abierto para poder elegir otra').toBeTrue();
  });

  it('se cierra con Escape y al moverse la página', () => {
    componente.abrirEn(raton(10, 10));
    componente.alPulsarEscape();
    expect(componente.abierto()).toBeFalse();

    componente.abrirEn(raton(10, 10));
    componente.alMoverse();
    expect(componente.abierto()).withContext(
      'si la página se mueve, el menú dejaría de señalar a lo que se eligió'
    ).toBeFalse();
  });

  it('pinta las opciones, con su separador y su color', () => {
    componente.abrirEn(raton(10, 10));
    fixture.detectChanges();

    const botones = Array.from(
      fixture.nativeElement.querySelectorAll('button[role=menuitem]') as NodeListOf<HTMLElement>
    );

    expect(botones.map(b => b.textContent?.trim()))
      .toEqual(['Crear nuevo evento', 'Ver eventos', 'Enviar a la papelera', 'No disponible']);

    expect(botones[2].className).toContain('text-destructive');
    expect(botones[3].hasAttribute('disabled')).toBeTrue();
  });
});
