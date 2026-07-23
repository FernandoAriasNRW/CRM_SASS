import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideChevronLeft, lucideChevronRight } from '@ng-icons/lucide';

export interface CalendarEvent {
  date: Date;
  title: string;
  color?: string;
}

@Component({
  selector: 'app-mini-calendar',
  standalone: true,
  imports: [CommonModule, NgIconComponent],
  viewProviders: [provideIcons({ lucideChevronLeft, lucideChevronRight })],
  templateUrl: './mini-calendar.component.html'
})
export class MiniCalendarComponent implements OnInit {
  @Input() events: CalendarEvent[] = [];

  currentDate = new Date();
  monthName = '';
  year = 0;
  
  weeks: (Date | null)[][] = [];
  weekDays = ['Do', 'Lu', 'Ma', 'Mi', 'Ju', 'Vi', 'Sa'];

  ngOnInit() {
    this.generateCalendar();
  }

  generateCalendar() {
    this.year = this.currentDate.getFullYear();
    const month = this.currentDate.getMonth();
    this.monthName = new Intl.DateTimeFormat('es-ES', { month: 'long' }).format(this.currentDate);

    const firstDayOfMonth = new Date(this.year, month, 1).getDay();
    const daysInMonth = new Date(this.year, month + 1, 0).getDate();

    this.weeks = [];
    let currentWeek: (Date | null)[] = [];

    // Padding for previous month
    for (let i = 0; i < firstDayOfMonth; i++) {
      currentWeek.push(null);
    }

    for (let i = 1; i <= daysInMonth; i++) {
      currentWeek.push(new Date(this.year, month, i));
      if (currentWeek.length === 7) {
        this.weeks.push(currentWeek);
        currentWeek = [];
      }
    }

    // Padding for next month
    if (currentWeek.length > 0) {
      while (currentWeek.length < 7) {
        currentWeek.push(null);
      }
      this.weeks.push(currentWeek);
    }
  }

  prevMonth() {
    this.currentDate = new Date(this.year, this.currentDate.getMonth() - 1, 1);
    this.generateCalendar();
  }

  nextMonth() {
    this.currentDate = new Date(this.year, this.currentDate.getMonth() + 1, 1);
    this.generateCalendar();
  }

  isToday(date: Date | null): boolean {
    if (!date) return false;
    const today = new Date();
    return date.getDate() === today.getDate() &&
           date.getMonth() === today.getMonth() &&
           date.getFullYear() === today.getFullYear();
  }

  getEventsForDate(date: Date | null): CalendarEvent[] {
    if (!date) return [];
    return this.events.filter(e => 
      e.date.getDate() === date.getDate() &&
      e.date.getMonth() === date.getMonth() &&
      e.date.getFullYear() === date.getFullYear()
    );
  }
}
