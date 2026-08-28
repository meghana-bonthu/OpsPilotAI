import { AsyncPipe, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import {
  BehaviorSubject,
  catchError,
  finalize,
  of,
  switchMap
} from 'rxjs';
import {
  Incident,
  IncidentPriority
} from '../../core/models/incident';

interface CreateIncidentRequest {
  title: string;
  description: string;
  priority: IncidentPriority;
}

@Component({
  selector: 'app-incident-list',
  imports: [AsyncPipe, DatePipe, ReactiveFormsModule],
  template: `
    <section class="page-heading">
      <div>
        <p class="eyebrow">INCIDENT QUEUE</p>
        <h2>Operational incidents</h2>
        <p>Review, prioritize, and resolve work that needs attention.</p>
      </div>

      <button type="button" (click)="toggleCreateForm()">
        {{ showCreateForm() ? 'Cancel' : 'Create incident' }}
      </button>
    </section>

    @if (showCreateForm()) {
      <section class="panel create-panel">
        <div class="table-heading">
          <div>
            <strong>Create a new incident</strong>
            <span>Provide enough detail for the response team.</span>
          </div>
        </div>

        <form [formGroup]="incidentForm" (ngSubmit)="submitIncident()">
          <div class="form-field">
            <label for="incident-title">Title</label>
            <input
              id="incident-title"
              type="text"
              formControlName="title"
              maxlength="160"
              placeholder="Briefly describe the issue"
            />

            @if (
              incidentForm.controls.title.touched &&
              incidentForm.controls.title.invalid
            ) {
              <small class="validation-error">
                Enter a title containing at least 5 characters.
              </small>
            }
          </div>

          <div class="form-field">
            <label for="incident-description">Description</label>
            <textarea
              id="incident-description"
              formControlName="description"
              maxlength="4000"
              rows="5"
              placeholder="Explain what happened and who is affected"
            ></textarea>

            @if (
              incidentForm.controls.description.touched &&
              incidentForm.controls.description.invalid
            ) {
              <small class="validation-error">
                Enter a description containing at least 10 characters.
              </small>
            }
          </div>

          <div class="form-field">
            <label for="incident-priority">Priority</label>
            <select id="incident-priority" formControlName="priority">
              @for (priority of priorities; track priority) {
                <option [value]="priority">{{ priority }}</option>
              }
            </select>
          </div>

          @if (saveError()) {
            <p class="request-error" role="alert">{{ saveError() }}</p>
          }

          <div class="form-actions">
            <button
              type="button"
              class="secondary-button"
              (click)="toggleCreateForm()"
              [disabled]="submitting()"
            >
              Cancel
            </button>

            <button type="submit" [disabled]="submitting()">
              {{ submitting() ? 'Creating…' : 'Create incident' }}
            </button>
          </div>
        </form>
      </section>
    }

    <section class="panel">
      <div class="table-heading">
        <strong>Current queue</strong>
        <span>Newest first</span>
      </div>

      @if (loadError()) {
        <p class="request-error" role="alert">{{ loadError() }}</p>
      }

      @if (incidents$ | async; as incidents) {
        @if (incidents.length) {
          <div class="incident-table" role="table">
            @for (incident of incidents; track incident.id) {
              <article class="incident-row" role="row">
                <div>
                  <strong>{{ incident.title }}</strong>
                  <small>{{ incident.createdAtUtc | date: 'medium' }}</small>
                </div>

                <span
                  class="pill priority-{{ incident.priority.toLowerCase() }}"
                >
                  {{ incident.priority }}
                </span>

                <span>{{ incident.status }}</span>
              </article>
            }
          </div>
        } @else if (!loadError()) {
          <p class="empty-state">
            No incidents yet. Create the first incident to begin the workflow.
          </p>
        }
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IncidentListComponent {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'https://localhost:7043/api/incidents';
  private readonly refreshIncidents = new BehaviorSubject<void>(undefined);

  protected readonly priorities: IncidentPriority[] = [
    'Low',
    'Medium',
    'High',
    'Critical'
  ];

  protected readonly showCreateForm = signal(false);
  protected readonly submitting = signal(false);
  protected readonly saveError = signal('');
  protected readonly loadError = signal('');

  protected readonly incidentForm = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(5),
        Validators.maxLength(160)
      ]
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(10),
        Validators.maxLength(4000)
      ]
    }),
    priority: new FormControl<IncidentPriority>('Medium', {
      nonNullable: true,
      validators: [Validators.required]
    })
  });

  protected readonly incidents$ = this.refreshIncidents.pipe(
    switchMap(() => {
      this.loadError.set('');

      return this.http.get<Incident[]>(this.apiUrl).pipe(
        catchError(() => {
          this.loadError.set(
            'Incidents could not be loaded. Confirm that the API is running.'
          );
          return of([]);
        })
      );
    })
  );

  protected toggleCreateForm(): void {
    this.showCreateForm.update(current => !current);
    this.saveError.set('');

    if (!this.showCreateForm()) {
      this.resetForm();
    }
  }

  protected submitIncident(): void {
    if (this.incidentForm.invalid) {
      this.incidentForm.markAllAsTouched();
      return;
    }

    const request: CreateIncidentRequest = this.incidentForm.getRawValue();

    this.submitting.set(true);
    this.saveError.set('');

    this.http
      .post<Incident>(this.apiUrl, request)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.resetForm();
          this.showCreateForm.set(false);
          this.refreshIncidents.next();
        },
        error: () => {
          this.saveError.set(
            'The incident could not be created. Review the information and try again.'
          );
        }
      });
  }

  private resetForm(): void {
    this.incidentForm.reset({
      title: '',
      description: '',
      priority: 'Medium'
    });
  }
}