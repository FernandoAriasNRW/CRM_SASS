import { Component, ElementRef, EventEmitter, OnDestroy, Output, ViewChild } from '@angular/core';

/** Lo que se usa del selector de picmo: abrirlo y cerrarlo, y oír la elección. */
interface EmojiPopup {
  toggle(): void;
  destroy(): void;
  addEventListener(event: 'emoji:select', handler: (selection: { emoji: string }) => void): void;
}

/**
 * El botón de emojis del editor de documentos.
 *
 * **picmo se carga al primer clic**, con `import()`. Son unos 108 kB que iban en el trozo del
 * editor para todo el que abría un documento, lo usara o no; así sólo los paga quien lo abre.
 */
@Component({
  selector: 'app-emoji-picker',
  standalone: true,
  imports: [],
  template: `
    <button #triggerBtn type="button" (click)="toggle()"
            class="p-2 text-muted-foreground hover:text-foreground transition-colors rounded hover:bg-muted"
            i18n-title title="Insertar emoji" i18n-aria-label aria-label="Insertar emoji">
      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-smile" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" x2="9.01" y1="9" y2="9"/><line x1="15" x2="15.01" y1="9" y2="9"/></svg>
    </button>
  `,
})
export class EmojiPickerComponent implements OnDestroy {
  @ViewChild('triggerBtn') triggerBtn!: ElementRef<HTMLButtonElement>;
  @Output() emojiSelected = new EventEmitter<string>();

  private picker: EmojiPopup | null = null;
  private loading: Promise<EmojiPopup> | null = null;
  private destroyed = false;

  async toggle(): Promise<void> {
    const picker = this.picker ?? await (this.loading ??= this.create());
    if (!this.destroyed) picker.toggle();
  }

  private async create(): Promise<EmojiPopup> {
    const { createPopup } = await import('@picmo/popup-picker');
    // Sin `triggerElement`: picmo pondría su propio manejador de clic en el botón, y ese clic ya
    // lo atiende `toggle()`. Con los dos, el primer clic lo abriría y lo cerraría a la vez.
    const picker = createPopup({}, {
      referenceElement: this.triggerBtn.nativeElement,
      position: 'bottom-start',
    }) as unknown as EmojiPopup;
    picker.addEventListener('emoji:select', selection => this.emojiSelected.emit(selection.emoji));
    this.picker = picker;
    return picker;
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.picker?.destroy();
  }
}
