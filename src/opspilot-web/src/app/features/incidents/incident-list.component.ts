import { AsyncPipe, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { catchError, of } from 'rxjs';
import { Incident } from '../../core/models/incident';

@Component({
  selector: 'app-incident-list',
  standalone: true,
  imports: [AsyncPipe, DatePipe],
  template: `
    <section class="page-heading"><div><p class="eyebrow">INCIDENT QUEUE</p><h2>Operational incidents</h2><p>Review, prioritize, and resolve work that needs attention.</p></div><button type="button">Create incident</button></section>
    <section class="panel">
      <div class="table-heading"><strong>Current queue</strong><span>Newest first</span></div>
      @if (incidents$ | async; as incidents) {
        @if (incidents.length) {
          <div class="incident-table" role="table">
            @for (incident of incidents; track incident.id) {
              <article class="incident-row" role="row">
                <div><strong>{{ incident.title }}</strong><small>{{ incident.createdAtUtc | date:'medium' }}</small></div>
                <span class="pill priority-{{ incident.priority.toLowerCase() }}">{{ incident.priority }}</span>
                <span>{{ incident.status }}</span>
              </article>
            }
          </div>
        } @else { <p class="empty-state">No incidents yet. Create the first incident to begin the workflow.</p> }
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IncidentListComponent {
  private readonly http = inject(HttpClient);
  protected readonly incidents$ = this.http.get<Incident[]>('https://localhost:7043/api/incidents').pipe(
    catchError(() => of([]))
  );
}
