using Identity.Application.Users;
using Identity.Domain.Users;
using NSubstitute;

namespace Identity.UnitTests.Users;

public sealed class ValidateCredentialsHandlerTests
{
    private readonly ILocalUserRepository _users = Substitute.For<ILocalUserRepository>();
    private readonly IPasswordVerifier _passwords = Substitute.For<IPasswordVerifier>();

    [Fact]
    public async Task Handle_WhenUserMissing_ShouldStillRunPasswordVerification()
    {
        // Arrange
        var handler = new ValidateCredentialsHandler(_users, _passwords);

        // Act
        var result = await handler.HandleAsync("nobody", "password", CancellationToken.None);

        // Assert
        Assert.Null(result);
        _passwords.Received(1).VerifyAgainstDummy("password");
    }

    [Fact]
    public async Task Handle_WhenPasswordRight_ShouldReturnUser()
    {
        // Arrange
        var user = LocalUser.Create("user", "hash");
        _users.FindByUsernameAsync("user", Arg.Any<CancellationToken>()).Returns(user);
        _passwords.Verify("hash", "password").Returns(true);
        var handler = new ValidateCredentialsHandler(_users, _passwords);

        // Act
        var result = await handler.HandleAsync("user", "password", CancellationToken.None);

        // Assert
        Assert.Same(user, result);
    }
}
