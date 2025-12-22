using Microsoft.AspNetCore.Mvc;
using Scribble.Business.Interfaces;
using Scribble.Business.Models;
using Scribble_API.DTOs;

namespace Scribble_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(new RegisterRequest
        {
            Username = request.Username,
            MobileNumber = request.MobileNumber
        });

        var response = new AuthResponseDto
        {
            Success = result.Success,
            Token = result.Token,
            Username = result.Username,
            MobileNumber = result.MobileNumber,
            UserId = result.UserId,
            Error = result.Error,
            ExpiresAt = result.ExpiresAt
        };

        if (!result.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(new LoginRequest
        {
            MobileNumber = request.MobileNumber
        });

        var response = new AuthResponseDto
        {
            Success = result.Success,
            Token = result.Token,
            Username = result.Username,
            MobileNumber = result.MobileNumber,
            UserId = result.UserId,
            Error = result.Error,
            ExpiresAt = result.ExpiresAt
        };

        if (!result.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("validate")]
    public async Task<ActionResult<AuthResponseDto>> ValidateToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized(new AuthResponseDto { Success = false, Error = "No token provided" });
        }

        var token = authHeader.Substring("Bearer ".Length);
        var result = await _authService.ValidateTokenAsync(token);

        var response = new AuthResponseDto
        {
            Success = result.Success,
            Token = result.Token,
            Username = result.Username,
            MobileNumber = result.MobileNumber,
            UserId = result.UserId,
            Error = result.Error
        };

        if (!result.Success)
        {
            return Unauthorized(response);
        }

        return Ok(response);
    }
}
