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
import { IdiomaService, type CodigoDeIdioma } from '../../core/idioma.service';
import { TemaService, TEMAS, type Tema } from '../../core/tema.service';
import { ToastService } from '../../shared/services/toast.service';
import { NotificationPreferencesComponent } from '../../shared/ui/notification-preferences.component';

/** Las secciones del perfil, en el orden en que se leen. */
type Seccion = 'cuenta' | 'apariencia' | 'seguridad' | 'notificaciones';

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
  private readonly avisos = inject(ToastService);

  readonly idiomas = inject(IdiomaService);
  readonly temas = inject(TemaService);
  readonly OPCIONES_DE_TEMA = TEMAS;

  readonly user = this.authStore.userInfo;

  readonly seccion = signal<Seccion>('cuenta');

  readonly SECCIONES: { clave: Seccion; nombre: string; icono: string }[] = [
    { clave: 'cuenta', nombre: $localize`Cuenta`, icono: 'lucideUser' },
    { clave: 'apariencia', nombre: $localize`Apariencia e idioma`, icono: 'lucidePalette' },
    { clave: 'seguridad', nombre: $localize`Seguridad`, icono: 'lucideLock' },
    { clave: 'notificaciones', nombre: $localize`Avisos`, icono: 'lucideBell' }
  ];

  // ── Cuenta ────────────────────────────────────────────────────────────────
  profileName = '';
  profilePhone = '';
  profileBio = '';
  readonly savingProfile = signal(false);
  readonly cargando = signal(true);

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
      next: (datos) => {
        this.authStore.updateUserInfo(datos);
        this.profileName = datos.name ?? '';
        this.profilePhone = datos.phoneNumber ?? '';
        this.profileBio = datos.bio ?? '';
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  guardarCuenta(): void {
    this.savingProfile.set(true);

    this.api.put<Record<string, unknown>>('/users/me/profile', {
      name: this.profileName,
      phoneNumber: this.profilePhone,
      bio: this.profileBio
    }).subscribe({
      next: (datos) => {
        this.authStore.updateUserInfo(datos);
        this.avisos.success($localize`Perfil actualizado`);
        this.savingProfile.set(false);
      },
      error: () => this.savingProfile.set(false)
    });
  }

  // ── Apariencia ────────────────────────────────────────────────────────────

  elegirTema(tema: Tema): void {
    this.temas.elegir(tema);
  }

  cambiarIdioma(codigo: string): void {
    this.idiomas.cambiarA(codigo as CodigoDeIdioma);
  }

  // ── Seguridad ─────────────────────────────────────────────────────────────

  /**
   * Las comprobaciones se hacen aquí <b>y</b> en el servidor.
   *
   * Aquí para decirlo antes de mandar —quien escribe una contraseña de cuatro letras se entera al
   * momento— y allí porque es lo único que de verdad protege: el navegador se puede saltar.
   */
  cambiarContrasena(): void {
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
        this.avisos.success($localize`Contraseña cambiada`);
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
        this.passwordError.set(motivoDelError(err) ?? $localize`No se pudo cambiar la contraseña.`);
        this.savingPassword.set(false);
      }
    });
  }

  cerrarSesion(): void {
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
function motivoDelError(err: unknown): string | null {
  const cuerpo = (err as { error?: unknown })?.error;

  if (typeof cuerpo === 'string' && cuerpo.trim()) return cuerpo;
  if (typeof (cuerpo as { error?: unknown })?.error === 'string') return (cuerpo as { error: string }).error;

  return null;
}
