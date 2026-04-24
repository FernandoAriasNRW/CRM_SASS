import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ApiService } from '../../core/api.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { TicketCreateModalComponent, type Ticket } from './ticket-create-modal.component';
import { TicketDetailPanelComponent } from './ticket-detail-panel.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideRefreshCw, lucidePlus, lucideList,
  lucideLayoutDashboard, lucideFilter, lucideUser
} from '@ng-icons/lucide';

interface Column { key: string; label: string; badge: BadgeVariant; tickets: Ticket[]; }

const COLUMN_DEFS: Omit<Column, 'tickets'>[] = [
  { key: 'New',         label: 'Nuevo',       badge: 'secondary' },
  { key: 'In Progress', label: 'En progreso',  badge: 'default'   },
  { key: 'Resolved',    label: 'Resuelto',     badge: 'success'   },
  { key: 'Closed',      label: 'Cerrado',      badge: 'outline'   },
];

const STATUS_BADGE: Record<string, BadgeVariant> = {
  'New': 'secondary', 'In Progress': 'default', 'Resolved': 'success', 'Closed': 'outline'
};

@Component({
  selector: 'app-tickets',
  standalone: true,
  imports: [
    FormsModule, BadgeComponent, ButtonComponent,
    NgIconComponent, DragDropModule, TicketCreateModalComponent, TicketDetailPanelComponent
  ],
  viewProviders: [provideIcons({
    lucideRefreshCw, lucidePlus, lucideList,
    lucideLayoutDashboard, lucideFilter, lucideUser
  })],
  templateUrl: './tickets.component.html',
})
export class TicketsComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly showModal = signal(false);
  readonly selectedTicket = signal<Ticket | null>(null);
  readonly viewMode = signal<'board' | 'list'>('board');
  readonly isLoading = signal(false);

  // Filtros - usando signals para reactivity
  filterStatus = signal('');
  filterCategory = signal('');
  filterSearch = signal('');

  // Arrays mutables por columna para CDK drag-drop
  cols: Column[] = COLUMN_DEFS.map(c => ({ ...c, tickets: [] as Ticket[] }));
  readonly columnIds = COLUMN_DEFS.map(c => c.key);

  readonly allTickets = signal<Ticket[]>([]);

  readonly filteredList = computed(() => {
    let tickets = this.allTickets();
    if (this.filterStatus()) tickets = tickets.filter(t => t.status === this.filterStatus());
    if (this.filterCategory()) tickets = tickets.filter(t => t.category === this.filterCategory());
    if (this.filterSearch()) {
      const q = this.filterSearch().toLowerCase();
      tickets = tickets.filter(t =>
        t.subject.toLowerCase().includes(q) ||
        t.contactName.toLowerCase().includes(q) ||
        t.company.toLowerCase().includes(q)
      );
    }
    return tickets;
  });

  readonly totalVisible = computed(() => this.filteredList().length);

  readonly categories = computed(() =>
    [...new Set(this.allTickets().map(t => t.category).filter(Boolean))]
  );

  readonly statuses = ['New', 'In Progress', 'Resolved', 'Closed'];

  statusBadge(status: string): BadgeVariant { return STATUS_BADGE[status] ?? 'outline'; }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.isLoading.set(true);
    this.api.get<Ticket[]>('/tickets').subscribe({
      next: tickets => {
        this.cols = COLUMN_DEFS.map(c => ({
          ...c, tickets: tickets.filter(t => t.status === c.key)
        }));
        this.allTickets.set(tickets);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  applyFilters(): void {
    const filtered = this.allTickets().filter(t => {
      if (this.filterStatus() && t.status !== this.filterStatus()) return false;
      if (this.filterCategory() && t.category !== this.filterCategory()) return false;
      if (this.filterSearch()) {
        const q = this.filterSearch().toLowerCase();
        if (!t.subject.toLowerCase().includes(q) &&
            !t.contactName.toLowerCase().includes(q) &&
            !t.company.toLowerCase().includes(q)) return false;
      }
      return true;
    });
    this.cols = COLUMN_DEFS.map(c => ({
      ...c, tickets: filtered.filter(t => t.status === c.key)
    }));
  }

  clearFilters(): void {
    this.filterStatus.set('');
    this.filterCategory.set('');
    this.filterSearch.set('');
    this.load();
  }

  openDetail(ticket: Ticket): void {
    this.selectedTicket.set(ticket);
  }

  onTicketUpdated(updated: Ticket): void {
    this.allTickets.update(tickets => tickets.map(t => t.id === updated.id ? updated : t));
    this.cols = this.cols.map(c => ({
      ...c, tickets: c.tickets.map(t => t.id === updated.id ? updated : t)
    }));
  }

  onTicketCreated(ticket: Ticket): void {
    const col = this.cols.find(c => c.key === 'New');
    if (col) col.tickets.unshift(ticket);
    this.allTickets.update(tickets => [ticket, ...tickets]);
  }

  drop(event: CdkDragDrop<Ticket[]>, targetKey: string): void {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      const ticket = event.previousContainer.data[event.previousIndex];
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex
      );
      this.allTickets.update(tickets =>
        tickets.map(t => t.id === ticket.id ? { ...t, status: targetKey } : t)
      );
      this.api.patch(`/tickets/${ticket.id}`, { status: targetKey }).subscribe();
    }
  }
}
