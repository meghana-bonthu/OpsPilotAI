import { TestBed } from '@angular/core/testing';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { IncidentListComponent } from './incident-list.component';

describe('IncidentListComponent', () => {
  let httpTestingController: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IncidentListComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create', () => {
    const fixture =
      TestBed.createComponent(IncidentListComponent);

    fixture.detectChanges();

    const request = httpTestingController.expectOne(
      'https://localhost:7043/api/incidents'
    );

    expect(request.request.method).toBe('GET');

    request.flush([]);

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should load and display incidents', () => {
    const fixture =
      TestBed.createComponent(IncidentListComponent);

    fixture.detectChanges();

    const request = httpTestingController.expectOne(
      'https://localhost:7043/api/incidents'
    );

    request.flush([
      {
        id: '11111111-1111-1111-1111-111111111111',
        title: 'Database connectivity issue',
        description: 'Users cannot connect to the database.',
        priority: 'High',
        status: 'New',
        createdAtUtc: '2026-09-02T01:00:00Z'
      }
    ]);

    fixture.detectChanges();

    expect(
      fixture.nativeElement.textContent
    ).toContain('Database connectivity issue');

    expect(
      fixture.nativeElement.textContent
    ).toContain('High');

    expect(
      fixture.nativeElement.textContent
    ).toContain('New');
  });

  it('should show an empty state when no incidents exist', () => {
    const fixture =
      TestBed.createComponent(IncidentListComponent);

    fixture.detectChanges();

    const request = httpTestingController.expectOne(
      'https://localhost:7043/api/incidents'
    );

    request.flush([]);

    fixture.detectChanges();

    expect(
      fixture.nativeElement.textContent
    ).toContain(
      'No incidents yet. Create the first incident to begin the workflow.'
    );
  });
});