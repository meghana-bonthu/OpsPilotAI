import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { Incident } from '../models/incident';
import {
  CreateIncidentRequest,
  IncidentService,
  UpdateIncidentStatusRequest
} from './incident.service';

describe('IncidentService', () => {
  let service: IncidentService;
  let httpTestingController: HttpTestingController;

  const apiUrl = 'https://localhost:7043/api/incidents';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        IncidentService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(IncidentService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should load incidents', () => {
    const incidents: Incident[] = [
      {
        id: '11111111-1111-1111-1111-111111111111',
        title: 'Database connectivity issue',
        description: 'Users cannot connect to the database.',
        priority: 'High',
        status: 'New',
        createdAtUtc: '2026-09-02T01:00:00Z'
      }
    ];

    service.getIncidents().subscribe(result => {
      expect(result).toEqual(incidents);
    });

    const request = httpTestingController.expectOne(apiUrl);

    expect(request.request.method).toBe('GET');

    request.flush(incidents);
  });

  it('should create an incident', () => {
    const createRequest: CreateIncidentRequest = {
      title: 'Application unavailable',
      description: 'The application is unavailable to users.',
      priority: 'Critical'
    };

    const createdIncident: Incident = {
      id: '22222222-2222-2222-2222-222222222222',
      ...createRequest,
      status: 'New',
      createdAtUtc: '2026-09-02T02:00:00Z'
    };

    service.createIncident(createRequest).subscribe(result => {
      expect(result).toEqual(createdIncident);
    });

    const request = httpTestingController.expectOne(apiUrl);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(createRequest);

    request.flush(createdIncident);
  });

  it('should update incident status', () => {
    const incidentId =
      '33333333-3333-3333-3333-333333333333';

    const statusRequest: UpdateIncidentStatusRequest = {
      status: 'Triaged'
    };

    const updatedIncident: Incident = {
      id: incidentId,
      title: 'Network latency',
      description: 'Users are experiencing network latency.',
      priority: 'Medium',
      status: 'Triaged',
      createdAtUtc: '2026-09-02T03:00:00Z'
    };

    service
      .updateStatus(incidentId, statusRequest)
      .subscribe(result => {
        expect(result).toEqual(updatedIncident);
      });

    const request = httpTestingController.expectOne(
      `${apiUrl}/${incidentId}/status`
    );

    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual(statusRequest);

    request.flush(updatedIncident);
  });
});