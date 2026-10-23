using System.Globalization;
using System.Text;

namespace Scrobblix.Application.ReadModels;

public static class HistoryCsvExporter
{
    public static string Export(IEnumerable<RecentPlay> plays)
    {
        ArgumentNullException.ThrowIfNull(plays);
        var output = new StringBuilder();
        output.AppendLine("started_utc,artist,title,album,duration_ms,confirmed_played_ms,eligible,reason,artwork_source,album_artist,track_number,release_year,recording_mbid,release_mbid");
        foreach (var play in plays)
        {
            var fields = new[]
            {
                Field(play.StartedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                Field(play.Artist), Field(play.Title), Field(play.Album),
                Field(play.DurationMilliseconds?.ToString(CultureInfo.InvariantCulture)),
                Field(play.ConfirmedPlayedMilliseconds.ToString(CultureInfo.InvariantCulture)),
                Field(play.EligibleUtc is null ? "false" : "true"),
                Field(play.TerminalReasonCode), Field(play.ArtworkSource), Field(play.AlbumArtist),
                Field(play.TrackNumber?.ToString(CultureInfo.InvariantCulture)),
                Field(play.ReleaseYear?.ToString(CultureInfo.InvariantCulture)),
                Field(play.RecordingMusicBrainzId), Field(play.ReleaseMusicBrainzId),
            };
            output.Append(string.Join(',', fields));
            output.AppendLine();
        }
        return output.ToString();
    }

    private static string Field(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' or '＝' or '＋' or '－' or '＠') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
