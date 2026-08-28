import { provideZoneChangeDetection } from "@angular/core";
import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, { providers: [provideZoneChangeDetection(),provideHttpClient()] })
  .catch(error => console.error(error));
