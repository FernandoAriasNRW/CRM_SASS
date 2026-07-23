import { Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthSignalStore } from '../../core/auth-signal.store';
import { HierarchySignalStore } from '../../core/hierarchy-signal.store';
import { CardComponent, CardContentComponent } from '../../shared/ui/card.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { 
  lucideLayoutDashboard, lucideFolderKanban, lucideCheckSquare,
  lucideTicket, lucideMessageSquare, lucideCalendar, lucideChartBar,
  lucideUsers, lucideWebhook, lucideFileText, lucideClock, lucideStar
} from '@ng-icons/lucide';
import { UpperCasePipe } from '@angular/common';
import { RecentViewsService } from '../../core/recent-views.service';
import { MiniCalendarComponent, CalendarEvent } from '../../shared/ui/mini-calendar.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CardComponent, CardContentComponent,
    NgIconComponent, RouterLink,
    MiniCalendarComponent
  ],
  viewProviders: [provideIcons({
    lucideLayoutDashboard, lucideFolderKanban, lucideCheckSquare,
    lucideTicket, lucideMessageSquare, lucideCalendar, lucideChartBar,
    lucideUsers, lucideWebhook, lucideFileText, lucideClock, lucideStar
  })],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit {
  readonly authStore = inject(AuthSignalStore);
  readonly hierarchyStore = inject(HierarchySignalStore);
  readonly recentViewsStore = inject(RecentViewsService);

  showAllRecents = false;
  myEvents: CalendarEvent[] = [];

  toggleRecents() {
    this.showAllRecents = !this.showAllRecents;
  }

  ngOnInit(): void {
    // Initializing specific Home logic if needed
  }
}
