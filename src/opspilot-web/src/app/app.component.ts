import { ChangeDetectionStrategy, Component } from '@angular/core';
import { IncidentListComponent } from './features/incidents/incident-list.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [IncidentListComponent],
  template: `
    <header class="shell-header"><div><span class="eyebrow">OPERATIONS WORKSPACE</span><h1>OpsPilot AI</h1></div><span class="environment">Development</span></header>
    <main><app-incident-list /></main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {}
