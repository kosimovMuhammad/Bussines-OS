namespace BusinessOS.Application.Abstraction;

// Encrypts OAuth/WhatsApp access & refresh tokens at rest (AES-256-GCM), keyed by ENCRYPTION_KEY.
public interface ITokenCipher
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
