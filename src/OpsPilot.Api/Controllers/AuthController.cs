using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Contracts;
using OpsPilot.Api.Domain;
using OpsPilot.Api.Security;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenService jwtTokenService)
    : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request)
    {
        var existingUser =
            await userManager.FindByEmailAsync(request.Email);

        if (existingUser is not null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "An account with this email already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result =
            await userManager.CreateAsync(
                user,
                request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new ValidationProblemDetails(
                result.Errors
                    .GroupBy(error => error.Code)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Select(error => error.Description)
                            .ToArray())));
        }

        await userManager.AddToRoleAsync(
            user,
            "Reporter");

        var token =
            await jwtTokenService.CreateTokenAsync(user);

        return Ok(
            new AuthResponse(
                token.Token,
                token.ExpiresAtUtc));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request)
    {
        var user =
            await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return Unauthorized();
        }

        var passwordValid =
            await signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: false);

        if (!passwordValid.Succeeded)
        {
            return Unauthorized();
        }

        var token =
            await jwtTokenService.CreateTokenAsync(user);

        return Ok(
            new AuthResponse(
                token.Token,
                token.ExpiresAtUtc));
    }
}