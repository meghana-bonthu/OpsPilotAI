export type IncidentPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type IncidentStatus = 'New' | 'Triaged' | 'InProgress' | 'Resolved' | 'Closed' | 'Cancelled';

export interface Incident {
  id: string;
  title: string;
  description: string;
  priority: IncidentPriority;
  status: IncidentStatus;
  createdAtUtc: string;
}
