// AesFileCrypto.cs
// Simple console app to encrypt/decrypt a text file using AES-256-CBC.
// Password -> key derivation via PBKDF2 (Rfc2898DeriveBytes).
// A random salt and IV are generated per encryption and stored at the
// start of the output file so decryption can reconstruct the same key/IV.
//
// Usage:
//   dotnet run -- encrypt input.txt encrypted.bin "MyStrongPassword"
//   dotnet run -- decrypt encrypted.bin decrypted.txt "MyStrongPassword"

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

class Program
{
    private const int SaltSize = 16;       // 128-bit salt
    private const int IvSize = 16;         // 128-bit IV (AES block size)
    private const int KeySize = 32;        // 256-bit key
    private const int Iterations = 100_000; // PBKDF2 iterations

    public static int Main(string[] args)
    {
        if (args.Length != 4 || (args[0] != "encrypt" && args[0] != "decrypt"))
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- encrypt <inputFile> <outputFile> <password>");
            Console.WriteLine("  dotnet run -- decrypt <inputFile> <outputFile> <password>");
            return 1;
        }

        string mode = args[0];
        string inputPath = args[1];
        string outputPath = args[2];
        string password = args[3];

        try
        {
            if (mode == "encrypt")
            {
                EncryptFile(inputPath, outputPath, password);
                Console.WriteLine($"Encrypted '{inputPath}' -> '{outputPath}'");
            }
            else
            {
                DecryptFile(inputPath, outputPath, password);
                Console.WriteLine($"Decrypted '{inputPath}' -> '{outputPath}'");
            }
            return 0;
        }
        catch (CryptographicException)
        {
            Console.WriteLine("Decryption failed. Wrong password or corrupted file.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static void EncryptFile(string inputPath, string outputPath, string password)
    {
        byte[] plainBytes = File.ReadAllBytes(inputPath);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = DeriveKey(password, salt);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using MemoryStream cipherStream = new MemoryStream();
        using (ICryptoTransform encryptor = aes.CreateEncryptor())
        using (CryptoStream cryptoStream = new CryptoStream(cipherStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
        {
            cryptoStream.Write(plainBytes, 0, plainBytes.Length);
        }

        // Output layout: [salt][iv][ciphertext]
        using FileStream outputFile = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        outputFile.Write(salt, 0, salt.Length);
        outputFile.Write(aes.IV, 0, aes.IV.Length);
        cipherStream.Position = 0;
        cipherStream.CopyTo(outputFile);
    }

    static void DecryptFile(string inputPath, string outputPath, string password)
    {
        byte[] fileBytes = File.ReadAllBytes(inputPath);

        if (fileBytes.Length < SaltSize + IvSize)
            throw new CryptographicException("File is too short to contain valid salt/IV header.");

        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[IvSize];
        Array.Copy(fileBytes, 0, salt, 0, SaltSize);
        Array.Copy(fileBytes, SaltSize, iv, 0, IvSize);

        int cipherStart = SaltSize + IvSize;
        int cipherLength = fileBytes.Length - cipherStart;

        byte[] key = DeriveKey(password, salt);

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using MemoryStream inputCipherStream = new MemoryStream(fileBytes, cipherStart, cipherLength);
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        using CryptoStream cryptoStream = new CryptoStream(inputCipherStream, decryptor, CryptoStreamMode.Read);
        using MemoryStream plainStream = new MemoryStream();
        cryptoStream.CopyTo(plainStream);

        File.WriteAllBytes(outputPath, plainStream.ToArray());
    }

    static byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }
}