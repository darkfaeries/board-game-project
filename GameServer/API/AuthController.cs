using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;

using GameServer;

public record AuthRequestDto(string Username, string Password);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IRepository<User> _userRepo;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthController(IConfiguration configuration, IRepository<User> userRepo)
    {
        _configuration = configuration;
        _userRepo = userRepo;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(AuthRequestDto request)
    {
        var existingUser = _userRepo.Get(request.Username);
        if (existingUser != null)
        {
            return BadRequest("Username already exists.");
        }

        var user = new User { Username = request.Username };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _userRepo.Create(user);

        var token = GenerateJwtToken(user.Id.ToString(), request.Username);
        return Ok(token);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(AuthRequestDto request)
    {
        var user = _userRepo.Get(request.Username);
        if (user == null) return Unauthorized("Invalid username.");

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return Unauthorized("User does not have a password set.");
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized("Invalid password.");
        }

        var token = GenerateJwtToken(user.Id.ToString(), request.Username);
        return Ok(token);
    }

    private string GenerateJwtToken(string id, string username)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "User"),
            new Claim(ClaimTypes.Name, username)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}