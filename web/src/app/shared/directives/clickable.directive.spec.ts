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
  template: `<div uiClickable (click)="veces = veces + 1">Fila</div>`,
})
class AnfitrionComponent {
  veces = 0;
}

describe('ClickableDirective', () => {
  let fixture: ComponentFixture<AnfitrionComponent>;
  let elemento: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AnfitrionComponent] }).compileComponents();
    fixture = TestBed.createComponent(AnfitrionComponent);
    fixture.detectChanges();
    elemento = fixture.nativeElement.querySelector('div');
  });

  it('se anuncia como botón', () => {
    expect(elemento.getAttribute('role')).toBe('button');
  });

  it('es alcanzable con el tabulador', () => {
    expect(elemento.getAttribute('tabindex')).toBe('0');
  });

  it('Enter activa el mismo manejador que el ratón', () => {
    elemento.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.veces).toBe(1);
  });

  it('Espacio activa el mismo manejador que el ratón', () => {
    elemento.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.veces).toBe(1);
  });

  it('Espacio no desplaza la página', () => {
    const evento = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true });

    elemento.dispatchEvent(evento);

    expect(evento.defaultPrevented).toBeTrue();
  });

  it('el clic del ratón sigue funcionando una sola vez', () => {
    // Reenviar la pulsación como click no debe provocar recursión ni doble disparo.
    elemento.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.veces).toBe(1);
  });
});
