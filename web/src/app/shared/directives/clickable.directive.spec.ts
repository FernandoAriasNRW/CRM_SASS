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

  it('announces itself as a button', () => {
    expect(element.getAttribute('role')).toBe('button');
  });

  it('is reachable with Tab', () => {
    expect(element.getAttribute('tabindex')).toBe('0');
  });

  it('Enter fires the same handler as the mouse', () => {
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.times).toBe(1);
  });

  it('Space fires the same handler as the mouse', () => {
    element.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.times).toBe(1);
  });

  it('Space does not scroll the page', () => {
    const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true });

    element.dispatchEvent(event);

    expect(event.defaultPrevented).toBeTrue();
  });

  it('a mouse click still fires only once', () => {
    // Reenviar la pulsación como click no debe provocar recursión ni doble disparo.
    element.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.times).toBe(1);
  });
});
