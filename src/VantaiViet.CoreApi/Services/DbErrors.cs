using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace VantaiViet.CoreApi.Services;

internal static class DbErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
