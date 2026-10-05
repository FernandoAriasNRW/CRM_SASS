import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { TagFieldComponent } from './tag-field.component';
import { ApiService } from '../../core/api.service';
import { type TagItem } from '../services/tags.service';

/**
 * El campo de etiquetas enseña las que tiene el elemento con su nombre y color, agrupa el selector
 * por categoría y avisa con la lista nueva al marcar o quitar una. No guarda: eso es de la ficha.
 */
describe('TagFieldComponent', () => {
  const BUG: TagItem = { id: 'bug', name: 'Bug', colorHex: '#EF4444', category: 'WorkType', categoryLabel: 'Tipo de trabajo' };
  const IMPROVEMENT: TagItem = { id: 'mejora', name: 'Mejora', colorHex: '#4F46E5', category: 'WorkType', categoryLabel: 'Tipo de trabajo' };
  const VIP: TagItem = { id: 'vip', name: 'Cliente VIP', colorHex: '#F59E0B', category: 'Business', categoryLabel: 'Negocio' };

  let fixture: ComponentFixture<TagFieldComponent>;
  let api: jasmine.SpyObj<ApiService>;

  async function mount(tagIds: string[]): Promise<void> {
    fixture = TestBed.createComponent(TagFieldComponent);
    fixture.componentRef.setInput('tagIds', tagIds);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const text = (selector: string): string[] =>
    [...(fixture.nativeElement as HTMLElement).querySelectorAll(selector)].map(e => e.textContent?.trim() ?? '');

  beforeEach(async () => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['get']);
    api.get.and.returnValue(of([BUG, IMPROVEMENT, VIP]) as never);

    await TestBed.configureTestingModule({
      imports: [TagFieldComponent],
      providers: [{ provide: ApiService, useValue: api }],
    }).compileComponents();
  });

  it('asks for tags in the application language', async () => {
    await mount([]);
    expect(api.get).toHaveBeenCalledWith(jasmine.stringMatching(/^\/tags\?language=/));
  });

  it('shows the item tags and skips an id that no longer exists', async () => {
    await mount(['vip', 'borrada']);
    expect(text('[data-testid="tag-chip"]').map(t => t.replace('×', '').trim())).toEqual(['Cliente VIP']);
  });

  it('groups the picker by category', async () => {
    await mount([]);
    (fixture.nativeElement.querySelector('[data-testid="tag-picker-toggle"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const picker = fixture.nativeElement.querySelector('[data-testid="tag-picker"]') as HTMLElement;
    expect(picker.textContent).toContain('Tipo de trabajo');
    expect(picker.textContent).toContain('Negocio');
  });

  it('emits the new list when checking and unchecking', async () => {
    await mount(['bug']);
    const emitted: string[][] = [];
    fixture.componentInstance.tagIdsChange.subscribe(ids => emitted.push(ids));

    fixture.componentInstance.toggle('vip');
    fixture.componentInstance.toggle('bug');

    expect(emitted).toEqual([['bug', 'vip'], []]);
  });
});
