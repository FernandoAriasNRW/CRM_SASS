import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideChevronLeft, lucideChevronRight, lucidePlus, lucideX } from '@ng-icons/lucide';

interface CalendarEvent {
  id: string; title: string; description: string;
  startsAtUtc: string; endsAtUtc: string;
}

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, LabelComponent, NgIconComponent, DatePipe],
  viewProviders: [provideIcons({ lucideChevronLeft, lucideChevronRight, lucidePlus, lucideX })],
  templateUrl: './calendar.component.html',
})
export class CalendarComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly events = signal<CalendarEvent[]>([]);
  readonly currentDate = signal(new Date());
  readonly showModal = signal(false);

  newTitle = '';
  newDescription = '';
  newStartsAt = '';
  newEndsAt = '';
  saving = signal(false);

  readonly monthLabel = computed(() =>
    this.currentDate().toLocaleDateString('es', { month: 'long', year: 'numeric' })
  );

  readonly calendarDays = computed(() => {
    const d = this.currentDate();
    const year = d.getFullYear();
    const month = d.getMonth();
    const firstDay = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const days: (number | null)[] = Array(firstDay).fill(null);
    for (let i = 1; i <= daysInMonth; i++) days.push(i);
    return days;
  });

  eventsForDay(day: number | null): CalendarEvent[] {
    if (!day) return [];
    const d = this.currentDate();
    return this.events().filter(e => {
      const ed = new Date(e.startsAtUtc);
      return ed.getFullYear() === d.getFullYear() && ed.getMonth() === d.getMonth() && ed.getDate() === day;
    });
  }

  prevMonth(): void {
    const d = this.currentDate();
    this.currentDate.set(new Date(d.getFullYear(), d.getMonth() - 1, 1));
    this.load();
  }

  nextMonth(): void {
    const d = this.currentDate();
    this.currentDate.set(new Date(d.getFullYear(), d.getMonth() + 1, 1));
    this.load();
  }

  ngOnInit(): void { this.load(); }

  load(): void {
    const d = this.currentDate();
    const from = new Date(d.getFullYear(), d.getMonth(), 1).toISOString();
    const to = new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59).toISOString();
    this.api.get<CalendarEvent[]>('/calendar/events', { from, to }).subscribe({
      next: data => this.events.set(data),
      error: () => {},
    });
  }

  createEvent(): void {
    if (!this.newTitle || !this.newStartsAt || !this.newEndsAt) return;
    this.saving.set(true);
    this.api.post('/calendar/events', {
      title: this.newTitle, description: this.newDescription,
      startsAtUtc: new Date(this.newStartsAt).toISOString(),
      endsAtUtc: new Date(this.newEndsAt).toISOString(),
    }).subscribe({
      next: () => { this.showModal.set(false); this.saving.set(false); this.load(); this.newTitle = ''; this.newDescription = ''; this.newStartsAt = ''; this.newEndsAt = ''; },
      error: () => this.saving.set(false),
    });
  }
}
