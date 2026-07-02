import { provideZoneChangeDetection, ErrorHandler } from "@angular/core";
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { GlobalErrorHandler } from './app/shared/error-boundary.component';

// Register global error handler
appConfig.providers.push({ provide: ErrorHandler, useClass: GlobalErrorHandler });

bootstrapApplication(AppComponent, {...appConfig, providers: [provideZoneChangeDetection(), ...appConfig.providers]})
  .catch((err) => console.error(err));
