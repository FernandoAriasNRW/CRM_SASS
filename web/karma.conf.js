// Configuración de Karma.
//
// Existe sólo para poder *comprobar* la cobertura, no para cambiar cómo se
// ejecutan las pruebas: el constructor de Angular ya traía una configuración
// implícita razonable, pero su informe de cobertura salía únicamente en HTML.
// Un HTML no lo puede leer un paso de CI, así que la cifra no se podía vigilar
// y podía bajar sin que nadie se enterara.
//
// `json-summary` añade coverage/web/coverage-summary.json, que sí es legible por
// una máquina; los umbrales de `check` hacen que Karma devuelva un código de
// salida distinto de cero cuando la cobertura baja. Sin eso, el paso de CI no
// podría fallar nunca — que es exactamente el fallo que tenía el backend.
module.exports = function (config) {
  config.set({
    basePath: '',
    frameworks: ['jasmine', '@angular-devkit/build-angular'],
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
      require('@angular-devkit/build-angular/plugins/karma'),
    ],
    client: {
      jasmine: {},
      clearContext: false, // deja visible el informe de Jasmine en el navegador
    },
    jasmineHtmlReporter: { suppressAll: true },

    coverageReporter: {
      dir: require('path').join(__dirname, './coverage/web'),
      subdir: '.',
      reporters: [
        { type: 'html' },
        { type: 'text-summary' },
        { type: 'json-summary' },
        { type: 'lcovonly' },
      ],
      // Los umbrales se fijan justo por debajo de lo medido hoy (47,3 % de
      // líneas, 46,4 % de sentencias, 37,7 % de ramas, 36,2 % de funciones).
      // Es un trinquete, no una meta: impide que la cobertura retroceda, y se
      // sube a mano conforme se gana terreno. Ponerlos en la meta final haría
      // que el build naciera rojo, y un build rojo permanente se ignora.
      check: {
        global: {
          statements: 45,
          lines: 45,
          branches: 35,
          functions: 35,
        },
      },
    },

    reporters: ['progress', 'kjhtml'],
    browsers: ['Chrome'],
    restartOnFileChange: true,
  });
};
