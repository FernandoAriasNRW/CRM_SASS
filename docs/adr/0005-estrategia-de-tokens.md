# ADR-0005: Separación entre token de acceso y token de refresco

**Estado:** Aceptado
**Fecha:** 2026-08-11

## Contexto

Durante la Fase 1 se encontró que el token de refresco se generaba con **exactamente los
mismos claims** que el de acceso: misma clave de firma, mismo issuer, misma audiencia.

La consecuencia es que el middleware de autenticación lo aceptaba como credencial de
acceso. Un token pensado para renovar sesión, con 7 días de vigencia, valía para llamar a
cualquier endpoint con el rol del usuario. No era una debilidad teórica: era un token de
acceso de larga duración entregado al cliente.

Además, el único claim variable era `exp`, con resolución de segundos: dos renovaciones en
el mismo segundo producían tokens **idénticos**, lo que impide revocarlos o auditarlos por
separado.

El código ya contenía un `GenerateSecureToken()` privado sin usar. La intención estaba; el
cableado no.

## Decisión

Los dos tokens dejan de ser intercambiables:

- El de refresco lleva **claims mínimos**: identidad y poco más. No lleva rol.
- Ambos llevan un claim `token_type` con valor distinto.
- El middleware **rechaza** cualquier token cuyo `token_type` no sea el de acceso; el
  endpoint de renovación rechaza el que no sea de refresco.
- Cada token lleva un `jti` único, de modo que dos emisiones en el mismo instante son
  distinguibles y revocables por separado.
- El de refresco viaja en cookie `HttpOnly`; el de acceso vive en memoria en el cliente,
  nunca en `localStorage`.

## Opciones consideradas

**A: Claves de firma distintas para cada tipo.** La separación más fuerte: un token de
refresco ni siquiera valida como de acceso. Descartada por ahora por el coste de gestionar
y rotar dos claves; es la evolución natural si aparece un requisito de cumplimiento.

**B: Claim `token_type` verificado (elegida).** Una sola clave que gestionar y la
verificación es explícita en un punto. Depende de que la comprobación esté presente, así que
está cubierta por tests.

**C: Tokens de refresco opacos en base de datos.** Permite revocación inmediata y real, que
un JWT no da. Es lo correcto cuando haga falta cerrar sesiones al instante, pero exige una
consulta a base en cada renovación y una tabla que mantener.

## Consecuencias

**Más fácil:** un token de refresco filtrado ya no da acceso a la API. El `jti` abre la
puerta a auditar y revocar.

**Más difícil:** hay que mantener la verificación de `token_type` en dos sitios; si alguien
la quita, vuelve el problema. De ahí que esté cubierta por tests.

**Sigue abierto:** no hay revocación real. Un token de acceso robado es válido hasta que
expira (60 minutos). Reducir esa ventana o pasar a la opción C es la siguiente decisión, no
resuelta aquí.

## Consecuencia operativa

La clave JWT anterior estuvo versionada en el repositorio, que es público. **Debe darse por
comprometida y rotarse**: retirarla del código no basta.

## Acciones

1. [x] Claims mínimos en el token de refresco
2. [x] `jti` único por emisión
3. [x] `token_type` verificado en el middleware y en la renovación
4. [x] Tests que cubren el uso de un token de acceso como refresco y viceversa
5. [ ] **Rotar la clave JWT comprometida**
6. [ ] Decidir sobre revocación real (opción C) y ventana del token de acceso
