using pgaActivityTools.Common.OpenApi;
using pgaActivityTools.Models.Authentication;
using pgaActivityTools.Services.Authentication;

namespace pgaActivityTools.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/authentication")
            .WithTags(OpenApiTags.Authentication.ToString())
            .AllowAnonymous()
            .WithOpenApi();

        group.MapPost("/login", Login)
            .WithName("UserLogin");

        return routes;
    }

    private static IResult Login(LoginRequest request, IAuthenticationService authService)
    {
        if (!authService.ValidateCredentials(request.Username, request.Password))
        {
            return Results.Unauthorized();
        }

        string? token = authService.GenerateToken(request.Username);
        if (token == null)
        {
            return Results.Problem("Failed to generate token");
        }
        
        return Results.Ok(new { Token = token });
    }
}