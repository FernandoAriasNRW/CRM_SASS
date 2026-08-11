using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Identity.Domain.ValueObjects;

public sealed class Email : ValueObject
{
  public string Value { get; }

  private Email() { Value = null!; } // EF las rellena al materializar.

  private Email(string value)
  {
    Value = value.ToLowerInvariant();
  }

  public static Result<Email> Create(string email)
  {
    if (string.IsNullOrWhiteSpace(email))
      return Result<Email>.Failure("Email es requerido");

    var normalized = email.ToLowerInvariant();

    if (normalized.Length > 160)
      return Result<Email>.Failure("Email excede 160 caracteres");

    if (!IsValidEmailFormat(normalized))
      return Result<Email>.Failure("Formato de email inválido");

    return Result<Email>.Success(new Email(normalized));
  }

  private static bool IsValidEmailFormat(string email)
  {
    try
    {
      var addr = new System.Net.Mail.MailAddress(email);
      return addr.Address == email;
    }
    catch
    {
      return false;
    }
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Value;
  }

  public static implicit operator string(Email email) => email.Value;

  public static explicit operator Email(string email) => Create(email).Value!;
}

public sealed class PasswordHash : ValueObject
{
  public string Value { get; }
  public DateTime CreatedAtUtc { get; }

  private PasswordHash() { Value = null!; } // EF las rellena al materializar.

  public PasswordHash(string value, DateTime createdAtUtc)
  {
    Value = value;
    CreatedAtUtc = createdAtUtc;
  }

  private PasswordHash(string value)
  {
    Value = value;
    CreatedAtUtc = DateTime.UtcNow;
  }

  public static PasswordHash Create(string plainPassword)
  {
    if (string.IsNullOrWhiteSpace(plainPassword))
      throw new ArgumentException("Contraseña no puede estar vacía");

    if (plainPassword.Length < 6)
      throw new ArgumentException("Contraseña debe tener al menos 6 caracteres");

    var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword, BCrypt.Net.BCrypt.GenerateSalt(12));
    return new PasswordHash(hash);
  }

  public static bool Verify(string plainPassword, string hash)
  {
    try
    {
      return BCrypt.Net.BCrypt.Verify(plainPassword, hash);
    }
    catch
    {
      return false;
    }
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Value;
  }
}

public sealed class UserRole : Enumeration
{
  public static readonly UserRole Admin = new(1, "Admin");
  public static readonly UserRole Member = new(2, "Member");
  public static readonly UserRole Guest = new(3, "Guest");

  private UserRole() : base(0, string.Empty) { }

  public UserRole(int value, string name) : base(value, name)
  {
  }
}

public sealed class RefreshToken : ValueObject
{
  public string Token { get; }
  public DateTime ExpiresAtUtc { get; }
  public bool IsRevoked { get; private set; }
  public DateTime? RevokedAtUtc { get; private set; }

  private RefreshToken() { Token = null!; } // EF las rellena al materializar.

  private RefreshToken(string token, DateTime expiresAtUtc)
  {
    Token = token;
    ExpiresAtUtc = expiresAtUtc;
    IsRevoked = false;
  }

  public static RefreshToken Create()
  {
    var randomBytes = new byte[64];
    using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
    rng.GetBytes(randomBytes);
    var token = Convert.ToBase64String(randomBytes);
    var expires = DateTime.UtcNow.AddDays(7);
    return new RefreshToken(token, expires);
  }

  public bool IsExpired => DateTime.UtcNow > ExpiresAtUtc;
  public bool IsValid => !IsRevoked && !IsExpired;

  public void Revoke(string reason = "Revocado")
  {
    IsRevoked = true;
    RevokedAtUtc = DateTime.UtcNow;
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Token;
  }
}