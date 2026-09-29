namespace Reporting.Domain.Definitions;

/// <summary>
/// El tipo de un campo, que decide qué operadores tienen sentido y cómo se agrupa.
///
/// <c>Persona</c> y <c>Referencia</c> se distinguen de <c>Texto</c> porque su valor es un
/// identificador: se filtran por igualdad y nunca por «contiene», y al pintarlos hay que
/// resolverlos a un nombre.
/// </summary>
public enum FieldType { Text, Number, Date, Person, Reference }
