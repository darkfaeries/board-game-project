using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public record AuthRequestDto(string Username, string Password);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(AuthRequestDto request)
    {
        // 1. Здесь работаешь со своей сущностью Player (проверяешь бд, создаешь игрока)
        // var player = new Player { Username = request.Username, ... };
        // _db.Players.Add(player);
        // await _db.SaveChangesAsync();

        Guid playerId = Guid.NewGuid(); 

        var token = GenerateJwtToken(playerId.ToString(), request.Username);

        return Ok(new { Token = token });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(AuthRequestDto request)
    {
        // 1. Проверяешь существование игрока в бд
        // var player = await _db.Players.FirstOrDefaultAsync(p => p.Username == request.Username);
        // if (player == null) return Unauthorized();

        Guid playerId = Guid.NewGuid(); // ID найденного игрока

        // 2. Генерируем токен
        var token = GenerateJwtToken(playerId.ToString(), request.Username);

        return Ok(new { Token = token });
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