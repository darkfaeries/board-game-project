using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GameServer;

namespace WebAPI.API.V1;

[ApiController]
[Authorize]
[Route("api/sessions")]
public class SessionController(IGameStore store) : ControllerBase
{
    [HttpGet("{code}")]
    public IActionResult GetSession(string code)
    {
        var session = store.GetSession(code);
        if (session == null)
        {
            return NotFound();
        }
        return Ok(session);
    }

    [HttpGet("{code}/board")]
    public IActionResult GetBoard(string code)
    {
        var session = store.GetSession(code);
        if (session == null)
        {
            return NotFound();
        }
        return Ok(session.GetGameState().Board);
    }

    [HttpGet("{code}/players")]
    public IActionResult GetPlayers(string code)
    {
        var session = store.GetSession(code);
        if (session == null)
        {
            return NotFound();
        }
        return Ok(session.GetGameState().Players);
    }

    [HttpGet("{code}/players/{playerId}")]
    public IActionResult GetPlayer(string code, Guid playerId)
    {
        var session = store.GetSession(code);
        if (session == null)
        {
            return NotFound();
        }
        var player = session.GetGameState().Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null)
        {
            return NotFound();
        }
        return Ok(player);
    }

    [HttpGet]
    public IActionResult GetAllSessions()
    {
        var sessions = store.GetSessions();
        return Ok(sessions);
    }
}