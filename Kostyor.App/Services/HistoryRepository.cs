using Kostyor.Core.History;
using Microsoft.Data.Sqlite;

namespace Kostyor.App.Services;

/// <summary>
/// История встреч в SQLite <c>%APPDATA%\Kostyor\history.db</c> (ТЗ §4). Личные данные —
/// не коммитим (AGENTS §11). Всё обёрнуто в try — сбой БД не роняет приложение.
/// </summary>
public sealed class HistoryRepository
{
    private readonly string _connectionString;
    private readonly Logger? _log;

    public HistoryRepository(string? dbPath = null, Logger? log = null)
    {
        var path = dbPath ?? AppPaths.HistoryDb;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        _log = log;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS meetings (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    StartedAt     TEXT NOT NULL,
                    EndedAt       TEXT NOT NULL,
                    DurationSec   REAL NOT NULL,
                    TotalRub      TEXT NOT NULL,
                    TotalUsd      TEXT NOT NULL,
                    TotalEur      TEXT NOT NULL,
                    TotalCny      TEXT NOT NULL,
                    Headcount     INTEGER NOT NULL,
                    PersonHours   REAL NOT NULL,
                    Composition   TEXT NOT NULL,
                    WorthIt       INTEGER NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _log?.Error("Не удалось создать схему истории", ex);
        }
    }

    public long Add(MeetingRecord r)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO meetings
                  (StartedAt, EndedAt, DurationSec, TotalRub, TotalUsd, TotalEur, TotalCny, Headcount, PersonHours, Composition, WorthIt)
                VALUES ($started, $ended, $dur, $rub, $usd, $eur, $cny, $head, $ph, $comp, $worth);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$started", r.StartedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$ended", r.EndedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$dur", r.DurationSeconds);
            cmd.Parameters.AddWithValue("$rub", r.TotalRub.ToString(System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$usd", r.TotalUsd.ToString(System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$eur", r.TotalEur.ToString(System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$cny", r.TotalCny.ToString(System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$head", r.Headcount);
            cmd.Parameters.AddWithValue("$ph", r.PersonHours);
            cmd.Parameters.AddWithValue("$comp", r.Composition);
            cmd.Parameters.AddWithValue("$worth", (object?)(r.WorthIt is null ? null : (r.WorthIt.Value ? 1 : 0)) ?? DBNull.Value);
            var id = (long)(cmd.ExecuteScalar() ?? 0L);
            r.Id = id;
            return id;
        }
        catch (Exception ex)
        {
            _log?.Error("Не удалось сохранить встречу в историю", ex);
            return 0;
        }
    }

    public void SetWorthIt(long id, bool worthIt)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE meetings SET WorthIt = $w WHERE Id = $id";
            cmd.Parameters.AddWithValue("$w", worthIt ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _log?.Error("Не удалось записать оценку встречи", ex);
        }
    }

    public IReadOnlyList<MeetingRecord> All()
    {
        var list = new List<MeetingRecord>();
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM meetings ORDER BY StartedAt DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Read(reader));
        }
        catch (Exception ex)
        {
            _log?.Error("Не удалось прочитать историю", ex);
        }
        return list;
    }

    private static MeetingRecord Read(SqliteDataReader r)
    {
        decimal Dec(string col) => decimal.TryParse(r[col]?.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

        var worthObj = r["WorthIt"];
        bool? worth = worthObj is null || worthObj is DBNull ? null : Convert.ToInt64(worthObj) != 0;

        return new MeetingRecord
        {
            Id = Convert.ToInt64(r["Id"]),
            StartedAt = DateTimeOffset.Parse(r["StartedAt"]!.ToString()!, System.Globalization.CultureInfo.InvariantCulture),
            EndedAt = DateTimeOffset.Parse(r["EndedAt"]!.ToString()!, System.Globalization.CultureInfo.InvariantCulture),
            DurationSeconds = Convert.ToDouble(r["DurationSec"]),
            TotalRub = Dec("TotalRub"),
            TotalUsd = Dec("TotalUsd"),
            TotalEur = Dec("TotalEur"),
            TotalCny = Dec("TotalCny"),
            Headcount = Convert.ToInt32(r["Headcount"]),
            PersonHours = Convert.ToDouble(r["PersonHours"]),
            Composition = r["Composition"]?.ToString() ?? "",
            WorthIt = worth,
        };
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
