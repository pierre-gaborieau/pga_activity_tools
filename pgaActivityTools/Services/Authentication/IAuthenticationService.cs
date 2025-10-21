namespace pgaActivityTools.Services.Authentication;

public interface IAuthenticationService
{
    bool ValidateCredentials(string username, string password);
    string? GenerateToken(string username);
}
