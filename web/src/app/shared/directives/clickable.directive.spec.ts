import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClickableDirective } from './clickable.directive';

/**
 * El linter no puede comprobar esta directiva: `click-events-have-key-events` analiza la
 * plantilla y busca un `(keydown)` escrito allí, así que no ve el que aporta la
 * directiva y sigue avisando. Estas pruebas son la verificación real de que el teclado
 * funciona.
 */
@Component({
  standalone: true,
  imports: [ClickableDirective],
  template: `<div appClickable (click)="times = times + 1">Fila</div>`,
})
class AnfitrionComponent {
  times = 0;
}

describe('ClickableDirective', () => {
  let fixture: ComponentFixture<AnfitrionComponent>;
  let element: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AnfitrionComponent] }).compileComponents();
    fixture = TestBed.createComponent(AnfitrionComponent);
    fixture.detectChanges();
    element = fixture.nativeElement.querySelector('div');
  });

  it('se anuncia como botón', () => {
    expect(element.getAttribute('role')).toBe('button');
  });

  it('es alcanzable con el tabulador', () => {
    expect(element.getAttribute('tabindex')).toBe('0');
  });

  it('Enter activa el mismo manejador que el ratón', () => {
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.times).toBe(1);
  });

  it('Espacio activa el mismo manejador que el ratón', () => {
    element.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.times).toBe(1);
  });

  it('Espacio no desplaza la página', () => {
    const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true });

    element.dispatchEvent(event);

    expect(event.defaultPrevented).toBeTrue();
  });

  it('el clic del ratón sigue funcionando una sola vez', () => {
    // Reenviar la pulsación como click no debe provocar recursión ni doble disparo.
    element.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.times).toBe(1);
  });
});
