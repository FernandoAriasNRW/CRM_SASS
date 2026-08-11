// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

/**
 * Línea base de lint.
 *
 * El proyecto arrancó sin linter, así que activarlo en modo estricto produce
 * ~270 fallos de golpe y bloquearía el CI desde el primer día. La estrategia es:
 *
 *   - `error`  para lo que indica un defecto real o rompe una convención
 *              que ya se respeta en casi todo el código.
 *   - `warn`   para la deuda heredada, que se mide y se va reduciendo por fases.
 *
 * Las reglas marcadas como deuda se escalan a `error` cuando su contador llega
 * a cero. Las de accesibilidad (a11y) se escalan al cerrar la fase de a11y;
 * son 64 avisos y representan el grueso del trabajo pendiente de WCAG.
 */
module.exports = tseslint.config(
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...tseslint.configs.stylistic,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'app', style: 'camelCase' },
      ],

      // ── Deuda heredada: TypeScript ──────────────────────────────────────
      '@typescript-eslint/no-explicit-any': 'warn',        // 84
      '@typescript-eslint/no-unused-vars': 'warn',         // 41
      '@typescript-eslint/no-empty-function': 'warn',      // 20
      '@typescript-eslint/no-inferrable-types': 'warn',    //  8
      '@typescript-eslint/array-type': 'warn',             //  1

      // ── Deuda heredada: convenciones Angular ────────────────────────────
      // 14 componentes usan un selector sin prefijo `app`.
      '@angular-eslint/component-selector': 'warn',
      '@angular-eslint/prefer-inject': 'warn',             //  5
      '@angular-eslint/no-output-native': 'warn',          //  3
      '@angular-eslint/no-output-on-prefix': 'warn',       //  2
      '@angular-eslint/use-lifecycle-interface': 'warn',   //  1
      '@angular-eslint/no-empty-lifecycle-method': 'warn', //  1
    },
  },
  {
    files: ['**/*.html'],
    extends: [
      ...angular.configs.templateRecommended,
      ...angular.configs.templateAccessibility,
    ],
    rules: {
      // ── Deuda heredada: plantillas ──────────────────────────────────────
      '@angular-eslint/template/prefer-control-flow': 'warn',        // 33

      // ── Accesibilidad: 64 avisos, el grueso del trabajo WCAG ────────────
      '@angular-eslint/template/click-events-have-key-events': 'warn', // 23
      '@angular-eslint/template/interactive-supports-focus': 'warn',   // 23
      '@angular-eslint/template/label-has-associated-control': 'warn', // 18
    },
  },
);
