import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PaginationComponent, type PaginationState } from './pagination.component';

/**
 * El paginado, y sobre todo <b>que sus iconos se dibujen</b>.
 *
 * Los cuatro botones de navegación usaban iconos que el componente no declaraba, y `ng-icon` no
 * falla cuando no encuentra uno: deja el hueco y escribe un aviso en la consola. Se veían cuatro
 * cuadros en blanco, que parecen un error de carga y nadie se atreve a pulsar.
 *
 * Por eso la comprobación es que hay un `<svg>` dentro de cada uno, y no que el botón existe: el
 * botón existía.
 */
describe('PaginationComponent', () => {
  let fixture: ComponentFixture<PaginationComponent>;
  let componente: PaginationComponent;

  const estado = (parcial: Partial<PaginationState> = {}): PaginationState => ({
    page: 2,
    pageSize: 25,
    totalCount: 46,
    totalPages: 2,
    hasPreviousPage: true,
    hasNextPage: false,
    ...parcial
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PaginationComponent] }).compileComponents();

    fixture = TestBed.createComponent(PaginationComponent);
    componente = fixture.componentInstance;
    fixture.componentRef.setInput('state', estado());
    fixture.detectChanges();
  });

  it('dibuja los cuatro iconos de navegación', () => {
    const iconos = fixture.nativeElement.querySelectorAll('ng-icon svg');

    expect(iconos.length).withContext(
      'primera, anterior, siguiente y última. Sin declararlos, `ng-icon` deja el hueco y salen ' +
      'cuatro cuadros en blanco'
    ).toBe(4);
  });

  it('no se pinta con una sola página', () => {
    fixture.componentRef.setInput('state', estado({ totalPages: 1 }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('button').length).toBe(0);
  });

  it('desactiva lo que no lleva a ninguna parte', () => {
    const botones = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>
    );

    // En la última página, «siguiente» y «última» no pueden pulsarse.
    expect(botones.filter(b => b.disabled).length).toBeGreaterThan(0);
  });
});
