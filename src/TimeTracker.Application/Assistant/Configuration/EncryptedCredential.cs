namespace TimeTracker.Application.Assistant.Configuration;

public sealed record EncryptedCredential(byte[] Ciphertext, byte[] Nonce, short KeyVersion);
