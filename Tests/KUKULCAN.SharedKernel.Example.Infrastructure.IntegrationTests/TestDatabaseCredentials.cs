using System.Security.Cryptography;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

/// <summary>
/// Generates short-lived credentials for disposable Testcontainers databases.
/// No usable database password or username is stored in the repository.
/// </summary>
internal static class TestDatabaseCredentials
{
    private const string UppercaseLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowercaseLetters = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*_-+";
    private const string AllCharacters = UppercaseLetters + LowercaseLetters + Digits + Symbols;

    public static string GeneratePassword(int length = 40)
    {
        if (length < 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), "A database password must contain at least four characters.");
        }

        var password = new char[length];
        var requiredGroups = new[]
        {
            UppercaseLetters,
            LowercaseLetters,
            Digits,
            Symbols
        };

        for (var index = 0; index < requiredGroups.Length; index++)
        {
            password[index] = GetRandomCharacter(requiredGroups[index]);
        }

        for (var index = requiredGroups.Length; index < password.Length; index++)
        {
            password[index] = GetRandomCharacter(AllCharacters);
        }

        // Shuffle the required character groups so their positions are unpredictable.
        for (var index = password.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (password[index], password[swapIndex]) = (password[swapIndex], password[index]);
        }

        return new string(password);
    }

    public static string GenerateUserName()
        => $"kukulcan_test_{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";

    private static char GetRandomCharacter(string characterSet)
        => characterSet[RandomNumberGenerator.GetInt32(characterSet.Length)];
}
