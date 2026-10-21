using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.Abstractions;
using Scrobblix.Application.Providers;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteProviderAccountStore(string connectionString) : IProviderAccountStore
{
    public async Task UpsertAsync(ProviderAccount account, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounts (
                account_id, provider, remote_username, credential_resource, is_connected,
                connected_utc, created_utc, updated_utc)
            VALUES ($accountId, $provider, $userName, $credentialResource, $isConnected,
                $connectedUtc, $createdUtc, $updatedUtc)
            ON CONFLICT(account_id) DO UPDATE SET
                provider = excluded.provider,
                remote_username = excluded.remote_username,
                credential_resource = excluded.credential_resource,
                is_connected = excluded.is_connected,
                connected_utc = excluded.connected_utc,
                updated_utc = excluded.updated_utc;
            """;
        var nowUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        command.Parameters.AddWithValue("$accountId", account.AccountId);
        command.Parameters.AddWithValue("$provider", account.Provider);
        command.Parameters.AddWithValue("$userName", account.UserName);
        command.Parameters.AddWithValue("$credentialResource", account.CredentialResource);
        command.Parameters.AddWithValue("$isConnected", account.IsConnected ? 1 : 0);
        command.Parameters.AddWithValue("$connectedUtc", account.ConnectedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$createdUtc", nowUtc);
        command.Parameters.AddWithValue("$updatedUtc", nowUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProviderAccount?> GetConnectedAsync(string provider, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT account_id, provider, remote_username, credential_resource, is_connected, connected_utc
            FROM accounts
            WHERE provider = $provider AND is_connected = 1
            ORDER BY updated_utc DESC, account_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$provider", provider);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProviderAccount(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4) == 1,
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public async Task DisconnectAsync(string provider, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE accounts
            SET is_connected = 0, updated_utc = $updatedUtc
            WHERE provider = $provider AND is_connected = 1;
            """;
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
