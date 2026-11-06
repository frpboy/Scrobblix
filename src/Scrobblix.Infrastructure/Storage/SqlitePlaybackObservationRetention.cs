using Microsoft.Data.Sqlite;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqlitePlaybackObservationRetention(string connectionString)
{
    // Deletes raw playback observations captured before cutoffUtc in bounded
    // transactions. Cancellation is honored between batches, so a cancelled or
    // failed run simply resumes pruning on its next invocation. Only row counts
    // are returned; no listening metadata leaves this method.
    public async Task<long> PruneExpiredAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, 5_000);
        var cutoff = cutoffUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        long totalDeleted = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                DELETE FROM playback_observations
                WHERE rowid IN (
                    SELECT rowid FROM playback_observations
                    WHERE captured_utc < $cutoff
                    LIMIT $batchSize);
                """;
            command.Parameters.AddWithValue("$cutoff", cutoff);
            command.Parameters.AddWithValue("$batchSize", batchSize);
            var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            totalDeleted += deleted;
            if (deleted < batchSize)
            {
                return totalDeleted;
            }
        }
    }
}
