import { Component, ElementRef, EventEmitter, Output, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { createPopup } from '@picmo/popup-picker';

@Component({
  selector: 'app-emoji-picker',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button #triggerBtn type="button" class="p-2 text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-200 transition-colors rounded hover:bg-zinc-100 dark:hover:bg-zinc-800" title="Insert Emoji">
      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-smile"><circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" x2="9.01" y1="9" y2="9"/><line x1="15" x2="15.01" y1="9" y2="9"/></svg>
    </button>
  `,
})
export class EmojiPickerComponent implements AfterViewInit, OnDestroy {
  @ViewChild('triggerBtn') triggerBtn!: ElementRef<HTMLButtonElement>;
  @Output() emojiSelected = new EventEmitter<string>();

  private picker: any;

  ngAfterViewInit() {
    this.picker = createPopup({}, {
      referenceElement: this.triggerBtn.nativeElement,
      triggerElement: this.triggerBtn.nativeElement,
      position: 'bottom-start'
    });

    this.picker.addEventListener('emoji:select', (selection: any) => {
      this.emojiSelected.emit(selection.emoji);
    });

    this.triggerBtn.nativeElement.addEventListener('click', () => {
      this.picker.toggle();
    });
  }

  ngOnDestroy() {
    if (this.picker) {
      this.picker.destroy();
    }
  }
}
