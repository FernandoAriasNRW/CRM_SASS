import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideBell, lucideGlobe, lucideLoaderCircle, lucideLock, lucideLogOut,
  lucidePalette, lucideSave, lucideUser
} from '@ng-icons/lucide';

import { AuthSignalStore } from '../../core/auth-signal.store';
import { ApiService } from '../../core/api.service';
import { LanguageService, type LanguageCode } from '../../core/language.service';
import { ThemeService, THEMES, type Theme } from '../../core/theme.service';
import { ToastService } from '../../shared/services/toast.service';
import { NotificationPreferencesComponent } from '../../shared/ui/notification-preferences.component';

/** Las secciones del perfil, en el orden en que se leen. */
type Section = 'cuenta' | 'apariencia' | 'seguridad' | 'notificaciones';

/**
 * El perfil: los datos de la persona y sus preferencias, todo en un sitio.
 *
 * <b>Antes era un cajón que se abría solo y daba un error al entrar.</b> Pedía `GET /users/me`,
 * que no existe —el usuario actual está en `/auth/users/me`— y el aviso decía «El recurso
 * solicitado no existe» encima de un formulario vacío. Cambiar la contraseña llamaba a
 * `/profile/password`, que tampoco existe: el comando estaba escrito desde el principio y ningún
 * endpoint lo exponía.
 *
 * Y era sólo eso: nombre, teléfono y biografía. El tema y el idioma no se podían elegir en
 * ninguna parte —los estilos oscuros existían y sólo los encendía un atajo de la paleta de
 * comandos, sin guardarse— y las preferencias de aviso vivían en otra pantalla.
 */
@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [FormsModule, NgIconComponent, NotificationPreferencesComponent],
  viewProviders: [provideIcons({
    lucideBell, lucideGlobe, lucideLoaderCircle, lucideLock, lucideLogOut,
    lucidePalette, lucideSave, lucideUser
  })],
  templateUrl: './profile.component.html'
})
export class ProfileComponent implements OnInit {
  readonly authStore = inject(AuthSignalStore);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly languages = inject(LanguageService);
  readonly themes = inject(ThemeService);
  readonly THEME_OPTIONS = THEMES;

  readonly user = this.authStore.userInfo;

  readonly section = signal<Section>('cuenta');

  readonly SECTIONS: { key: Section; name: string; icon: string }[] = [
    { key: 'cuenta', name: $localize`Cuenta`, icon: 'lucideUser' },
    { key: 'apariencia', name: $localize`Apariencia e idioma`, icon: 'lucidePalette' },
    { key: 'seguridad', name: $localize`Seguridad`, icon: 'lucideLock' },
    { key: 'notificaciones', name: $localize`Avisos`, icon: 'lucideBell' }
  ];

  // ── Cuenta ────────────────────────────────────────────────────────────────
  profileName = '';
  profilePhone = '';
  profileBio = '';
  readonly savingProfile = signal(false);
  readonly loading = signal(true);

  // ── Seguridad ─────────────────────────────────────────────────────────────
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  readonly passwordError = signal('');
  readonly savingPassword = signal(false);

  ngOnInit(): void {
    if (!this.authStore.isAuthenticated()) {
      void this.router.navigate(['/login']);
      return;
    }

    // `/auth/users/me`, no `/users/me`.
    //
    // La ruta buena cuelga del grupo de autenticación. La que había devolvía 404 y el
    // interceptor lo convertía en el aviso rojo que aparecía al abrir el perfil, con el
    // formulario en blanco detrás.
    this.api.get<{ name?: string; phoneNumber?: string; bio?: string }>('/auth/users/me').subscribe({
      next: (data) => {
        this.authStore.updateUserInfo(data);
        this.profileName = data.name ?? '';
        this.profilePhone = data.phoneNumber ?? '';
        this.profileBio = data.bio ?? '';
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  saveAccount(): void {
    this.savingProfile.set(true);

    this.api.put<Record<string, unknown>>('/users/me/profile', {
      name: this.profileName,
      phoneNumber: this.profilePhone,
      bio: this.profileBio
    }).subscribe({
      next: (data) => {
        this.authStore.updateUserInfo(data);
        this.toast.success($localize`Perfil actualizado`);
        this.savingProfile.set(false);
      },
      error: () => this.savingProfile.set(false)
    });
  }

  // ── Apariencia ────────────────────────────────────────────────────────────

  chooseTheme(theme: Theme): void {
    this.themes.choose(theme);
  }

  changeLanguage(code: string): void {
    this.languages.switchTo(code as LanguageCode);
  }

  // ── Seguridad ─────────────────────────────────────────────────────────────

  /**
   * Las comprobaciones se hacen aquí <b>y</b> en el servidor.
   *
   * Aquí para decirlo antes de mandar —quien escribe una contraseña de cuatro letras se entera al
   * momento— y allí porque es lo único que de verdad protege: el navegador se puede saltar.
   */
  changePassword(): void {
    this.passwordError.set('');

    if (!this.currentPassword || !this.newPassword || !this.confirmPassword) {
      this.passwordError.set($localize`Rellena los tres campos.`);
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.passwordError.set($localize`Las contraseñas no coinciden.`);
      return;
    }

    if (this.newPassword.length < 6) {
      this.passwordError.set($localize`La nueva contraseña necesita al menos 6 caracteres.`);
      return;
    }

    this.savingPassword.set(true);

    // `/users/me/password`. La pantalla llamaba a `/profile/password`, que no es ninguna ruta.
    this.api.put<void>('/users/me/password', {
      currentPassword: this.currentPassword,
      newPassword: this.newPassword
    }).subscribe({
      next: () => {
        this.toast.success($localize`Contraseña cambiada`);
        this.currentPassword = '';
        this.newPassword = '';
        this.confirmPassword = '';
        this.savingPassword.set(false);
      },
      error: (err) => {
        // El servidor dice por qué —«la contraseña actual no es correcta»—, y eso es más útil que
        // un mensaje genérico.
        //
        // Llega de dos formas según el endpoint: unos devuelven la cadena suelta y otros la
        // envuelven en `{ error }`. Leer sólo una dejaba el motivo dentro de la respuesta y en
        // pantalla el mensaje de repuesto, que no dice nada.
        this.passwordError.set(errorReason(err) ?? $localize`No se pudo cambiar la contraseña.`);
        this.savingPassword.set(false);
      }
    });
  }

  signOut(): void {
    this.api.post('/auth/logout', {}).subscribe({
      next: () => this.authStore.logout(),
      error: () => this.authStore.logout()
    });
  }
}

/**
 * El motivo que manda el servidor, venga como venga.
 *
 * `Results.BadRequest("texto")` serializa una cadena JSON suelta y `BadRequest(new { error })` un
 * objeto. Los dos conviven en esta API, así que se miran los dos antes de rendirse.
 */
function errorReason(err: unknown): string | null {
  const body = (err as { error?: unknown })?.error;

  if (typeof body === 'string' && body.trim()) return body;
  if (typeof (body as { error?: unknown })?.error === 'string') return (body as { error: string }).error;

  return null;
}
