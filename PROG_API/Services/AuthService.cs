// // Original source: Microsoft Learn - "Add JWT Bearer authentication in ASP.NET Core Web API"
// // URL: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay API authentication and session management
// // Modification: Added role-based authorization, JWT token generation with claims,
// // and session-independent authentication via bearer tokens
//
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PROG_API.Data;
using PROG_API.DTOs;
using PROG_API.Models;

namespace PROG_API.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new ValidationException("Password must be at least 6 characters long.");
        }

        var normalizedEmail = request.Email.Trim();
        var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail.ToLower(), ct);
        if (emailExists)
        {
            throw new ValidationException("A user with this email already exists.");
        }

        var normalizedRole = string.IsNullOrWhiteSpace(request.Role)
            ? "Participant"
            : request.Role.Trim();

        if (!string.Equals(normalizedRole, "Participant", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedRole, "Organiser", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Role must be either Organiser or Participant.");
        }

        normalizedRole = normalizedRole.Equals("Organiser", StringComparison.OrdinalIgnoreCase)
            ? "Organiser"
            : "Participant";

        var user = new Users
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            DateOfBirth = request.DateOfBirth,
            Role = normalizedRole,
            IsActive = true,
            PasswordHash = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = PasswordHasher.HashPassword(request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var expiresAt = DateTime.UtcNow.AddHours(2);
        return new AuthResponseDto
        {
            Token = GenerateJwtToken(user, expiresAt),
            ExpiresAt = expiresAt,
            User = ToUserDto(user)
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Email and password are required.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.Trim().ToLower(), ct);
        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("This account is inactive.");
        }

        var expiresAt = DateTime.UtcNow.AddHours(2);
        var token = GenerateJwtToken(user, expiresAt);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = ToUserDto(user)
        };
    }

    public async Task<UserDto> GetUserAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        return ToUserDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (request.Phone is not null)
        {
            user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        }

        if (request.DateOfBirth.HasValue)
        {
            user.DateOfBirth = request.DateOfBirth.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ToUserDto(user);
    }

    public static UserDto ToUserDto(Users user)
    {
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            Phone = user.Phone,
            DateOfBirth = user.DateOfBirth,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private string GenerateJwtToken(Users user, DateTime expiresAt)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "RaceDayDevelopmentKey-ReplaceInProduction123!";
        var issuer = _configuration["Jwt:Issuer"] ?? "RaceDayApi";
        var audience = _configuration["Jwt:Audience"] ?? "RaceDayClients";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
