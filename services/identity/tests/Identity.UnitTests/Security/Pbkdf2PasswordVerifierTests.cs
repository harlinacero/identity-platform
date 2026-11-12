using System.Buffers.Binary;
using System.Text;
using Identity.Infrastructure.Security;

namespace Identity.UnitTests.Security;

public sealed class Pbkdf2PasswordVerifierTests
{
    private const string Password = "a-readable-password";

    private readonly Pbkdf2PasswordVerifier _verifier = new();

    [Fact]
    public void Hash_WhenCreated_ShouldNotContainPasswordAndUseConfiguredIterations()
    {
        // Act
        var hash = _verifier.Hash(Password);

        // Assert
        var bytes = Convert.FromBase64String(hash);
        Assert.DoesNotContain(Password, hash);
        Assert.DoesNotContain(Password, Encoding.UTF8.GetString(bytes));

        // Identity V3 format: 1 byte version, 4 bytes PRF, then 4 bytes iteration count, big-endian.
        Assert.Equal(210_000, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(5, 4)));
    }

    [Fact]
    public void Verify_WhenPasswordRight_ShouldSucceed()
    {
        // Arrange
        var hash = _verifier.Hash(Password);

        // Act & Assert
        Assert.True(_verifier.Verify(hash, Password));
    }

    [Fact]
    public void Verify_WhenPasswordWrong_ShouldFail()
    {
        // Arrange
        var hash = _verifier.Hash(Password);

        // Act & Assert
        Assert.False(_verifier.Verify(hash, "another-password"));
    }
}
