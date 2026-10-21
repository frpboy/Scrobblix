namespace Scrobblix.Domain.Queue;

public static class ProviderActionIdempotencyKey
{
    public static string Create(string provider, string accountId, ProviderActionType actionType, string playId)
    {
        Validate(provider, nameof(provider));
        Validate(accountId, nameof(accountId));
        Validate(playId, nameof(playId));

        if (!Enum.IsDefined(actionType))
        {
            throw new ArgumentOutOfRangeException(nameof(actionType));
        }

        return string.Concat(
            Encode(provider),
            Encode(accountId),
            Encode(((int)actionType).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Encode(playId));
    }

    private static string Encode(string value) => string.Concat(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", value);

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new ArgumentException("An identifier must be non-empty and contain no control characters.", parameterName);
        }
    }
}
