namespace Tags.Application.DTOs;

/// <summary>
/// Una categoría de etiquetas. <c>Name</c> es el valor que se guarda en la etiqueta y se manda al
/// crearla; <c>Label</c>, cómo se muestra. En las predefinidas difieren («WorkType» / «Tipo de
/// trabajo»); en las personalizadas son el mismo. Sólo las personalizadas tienen <c>Id</c>.
/// <c>IsAutomatic</c> marca las que rellena el sistema y no admiten etiquetas a mano.
/// </summary>
public sealed record TagCategoryDto(Guid? Id, string Name, string Label, bool IsCustom, bool IsAutomatic);
