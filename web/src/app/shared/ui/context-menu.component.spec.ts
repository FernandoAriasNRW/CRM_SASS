import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ContextMenuComponent, type MenuOption } from './context-menu.component';

describe('MenuContextualComponent', () => {
  let fixture: ComponentFixture<ContextMenuComponent>;
  let component: ContextMenuComponent;

  const OPTIONS: MenuOption[] = [
    { key: 'crear', label: 'Crear nuevo evento' },
    { key: 'ver', label: 'Ver eventos' },
    { key: 'papelera', label: 'Enviar a la papelera', destructive: true, separatorBefore: true },
    { key: 'bloqueada', label: 'No disponible', disabled: true }
  ];

  const raton = (x: number, y: number) =>
    new MouseEvent('contextmenu', { clientX: x, clientY: y, cancelable: true });

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ContextMenuComponent] }).compileComponents();

    fixture = TestBed.createComponent(ContextMenuComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('options', OPTIONS);
    fixture.detectChanges();
  });

  it('empieza cerrado y se abre donde se pulsó', () => {
    expect(component.isOpen()).toBeFalse();

    component.openAt(raton(100, 120));
    fixture.detectChanges();

    expect(component.isOpen()).toBeTrue();
    expect(component.position()).toEqual({ x: 100, y: 120 });
  });

  /** Sin esto, el menú del calendario abriría además el día de la celda sobre la que se pulsó. */
  it('cancela el menú del navegador', () => {
    const event = raton(10, 10);
    component.openAt(event);

    expect(event.defaultPrevented).toBeTrue();
  });

  /**
   * El fallo que esto evita: pulsando en la última fila del mes, el menú se salía por abajo y sus
   * últimas opciones —las de borrar— quedaban fuera de la pantalla, inalcanzables.
   */
  it('se recoloca para no salirse de la ventana', () => {
    component.openAt(raton(window.innerWidth - 5, window.innerHeight - 5));

    const { x, y } = component.position();
    expect(x).toBeLessThan(window.innerWidth - 5);
    expect(y).toBeLessThan(window.innerHeight - 5);
  });

  it('avisa de la opción elegida y se cierra', () => {
    const chosenKeys: string[] = [];
    component.chosen.subscribe(c => chosenKeys.push(c));

    component.openAt(raton(10, 10));
    component.choose(OPTIONS[0]);

    expect(chosenKeys).toEqual(['crear']);
    expect(component.isOpen()).toBeFalse();
  });

  it('una opción deshabilitada no hace nada', () => {
    const chosenKeys: string[] = [];
    component.chosen.subscribe(c => chosenKeys.push(c));

    component.openAt(raton(10, 10));
    component.choose(OPTIONS[3]);

    expect(chosenKeys).toEqual([]);
    expect(component.isOpen()).withContext('el menú sigue abierto para poder elegir otra').toBeTrue();
  });

  it('se cierra con Escape y al moverse la página', () => {
    component.openAt(raton(10, 10));
    component.onEscape();
    expect(component.isOpen()).toBeFalse();

    component.openAt(raton(10, 10));
    component.onMove();
    expect(component.isOpen()).withContext(
      'si la página se mueve, el menú dejaría de señalar a lo que se eligió'
    ).toBeFalse();
  });

  it('pinta las opciones, con su separador y su color', () => {
    component.openAt(raton(10, 10));
    fixture.detectChanges();

    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button[role=menuitem]') as NodeListOf<HTMLElement>
    );

    expect(buttons.map(b => b.textContent?.trim()))
      .toEqual(['Crear nuevo evento', 'Ver eventos', 'Enviar a la papelera', 'No disponible']);

    expect(buttons[2].className).toContain('text-destructive');
    expect(buttons[3].hasAttribute('disabled')).toBeTrue();
  });
});
