using FluentAssertions;
using Identity.Domain.Entities;
using Xunit;

namespace UnitTests;

/// <summary>
/// La zona horaria de la organización: solo nombres IANA que el sistema conoce.
///
/// Los nombres de Windows («Romance Standard Time») se rechazan aunque .NET los acepte en Windows:
/// la API corre en Linux, y una zona guardada desde una máquina Windows dejaría de encontrarse.
/// </summary>
public sealed class OrganizationSettingsTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_new_organization_starts_in_utc()
        => OrganizationSettings.Create(Now, Guid.NewGuid()).TimeZone.Should().Be("UTC");

    [Theory]
    [InlineData("America/Bogota")]
    [InlineData("Europe/Madrid")]
    [InlineData("UTC")]
    [InlineData("  America/Mexico_City  ")]
    public void An_iana_zone_is_accepted(string name)
    {
        var settings = OrganizationSettings.Create(Now, Guid.NewGuid());

        settings.ChangeTimeZone(Now, name).IsSuccess.Should().BeTrue();
        settings.TimeZone.Should().Be(name.Trim());
    }

    [Theory]
    [InlineData("Romance Standard Time")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_rejected_and_the_zone_does_not_change(string? name)
    {
        var settings = OrganizationSettings.Create(Now, Guid.NewGuid());

        settings.ChangeTimeZone(Now, name).IsFailure.Should().BeTrue();
        settings.TimeZone.Should().Be("UTC");
    }

    [Fact]
    public void The_zone_is_the_real_one_eight_in_bogota_is_thirteen_utc()
    {
        OrganizationSettings.TryFindTimeZone("America/Bogota", out var bogota).Should().BeTrue();

        TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2026, 10, 12, 13, 0, 0, DateTimeKind.Utc), bogota)
            .Hour.Should().Be(8);
    }
}
