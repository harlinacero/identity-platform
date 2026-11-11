using System.Net.Sockets;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Identity.UnitTests.Persistence;

public sealed class DataStoreFailuresTests
{
    public static TheoryData<string, bool> PermanentErrors => new()
    {
        { PostgresErrorCodes.UniqueViolation, false },
        { PostgresErrorCodes.UniqueViolation, true },
        { PostgresErrorCodes.InsufficientPrivilege, false },
        { PostgresErrorCodes.InsufficientPrivilege, true },
        { PostgresErrorCodes.UndefinedTable, false },
        { PostgresErrorCodes.UndefinedTable, true },
    };

    public static TheoryData<string> TransientErrors => new()
    {
        nameof(RetryLimitExceededException),
        nameof(TimeoutException),
        nameof(NpgsqlException),
    };

    [Theory]
    [MemberData(nameof(PermanentErrors))]
    public void IsTransient_WhenPermanentDatabaseError_ShouldBeFalse(string sqlState, bool wrapped)
    {
        // Arrange
        Exception error = new PostgresException("permanent", "ERROR", "ERROR", sqlState);
        if (wrapped)
        {
            error = new DbUpdateException("save failed", error);
        }

        // Act
        var transient = DataStoreFailures.IsTransient(error);

        // Assert
        Assert.False(transient);
    }

    [Theory]
    [MemberData(nameof(TransientErrors))]
    public void IsTransient_WhenOutageOrTimeout_ShouldBeTrue(string kind)
    {
        // Arrange
        var outage = new NpgsqlException("connection refused", new SocketException((int)SocketError.ConnectionRefused));
        Exception error = kind switch
        {
            nameof(RetryLimitExceededException) => new RetryLimitExceededException("retries exhausted", outage),
            nameof(TimeoutException) => new InvalidOperationException("query failed", new TimeoutException()),
            _ => outage,
        };

        // Act
        var transient = DataStoreFailures.IsTransient(error);

        // Assert
        Assert.True(transient);
    }
}
