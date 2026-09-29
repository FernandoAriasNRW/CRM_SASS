namespace Tags.Application.BuiltIn;

/// <summary>Una etiqueta que trae el producto, con su nombre en los dos idiomas de la aplicación.</summary>
public sealed record BuiltInTag(string Key, string Category, string SpanishName, string EnglishName, string ColorHex)
{
    public string NameIn(string? language) => Languages.IsEnglish(language) ? EnglishName : SpanishName;
}
