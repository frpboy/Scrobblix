using System.Text;

namespace Scrobblix.Domain.Metadata;

public static class MetadataNormalizer
{
    public static NormalizedMetadataValue? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var builder = new StringBuilder(raw.Length);
        var previousWasWhitespace = false;

        foreach (var character in raw.Normalize(NormalizationForm.FormC))
        {
            if (char.IsControl(character) && !char.IsWhiteSpace(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && !previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        var payload = builder.ToString().TrimEnd();
        return payload.Length == 0
            ? null
            : new NormalizedMetadataValue(raw, payload, payload.ToUpperInvariant());
    }
}
