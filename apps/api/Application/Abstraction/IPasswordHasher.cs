namespace BusinessOS.Application.Abstraction;

public interface IPasswordHasher
{
    Task<string> HashAsync(string password);
    Task<bool> VerifyAsync(string hash, string password);
}
