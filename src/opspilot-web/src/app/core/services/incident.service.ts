import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

import {
  Incident,
  IncidentPriority,
  IncidentStatus
} from '../models/incident';

export interface CreateIncidentRequest {
  title: string;
  description: string;
  priority: IncidentPriority;
}

export interface UpdateIncidentStatusRequest {
  status: IncidentStatus;
}

@Injectable({
  providedIn: 'root'
})
export class IncidentService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'https://localhost:7043/api/incidents';

  getIncidents() {
    return this.http.get<Incident[]>(this.apiUrl);
  }

  createIncident(request: CreateIncidentRequest) {
    return this.http.post<Incident>(
      this.apiUrl,
      request
    );
  }

  updateStatus(
    incidentId: string,
    request: UpdateIncidentStatusRequest
  ) {
    return this.http.patch<Incident>(
      `${this.apiUrl}/${incidentId}/status`,
      request
    );
  }
}