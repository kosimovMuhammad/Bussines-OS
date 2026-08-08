using System.Text;

namespace BusinessOS.Application.Services;

// Shared by every Phase 6 import/export method. Field()'s escaping matches
// AuditLogService.ExportCsvAsync/ReportExportService's existing convention exactly (kept
// those two files' own private copies untouched rather than risking a refactor of
// already-tested code just for DRY) - this is the first place that also needs to READ csv,
// so ParseRows is new.
public static class CsvUtils
{
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    // Minimal RFC4180 reader: quoted fields, embedded commas/newlines, doubled-quote
    // escaping. Not a full CSV library (none is referenced in this project) - good enough
    // for round-tripping what Field() above produces and what a spreadsheet export
    // typically looks like.
    public static List<string[]> ParseRows(string content)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void EndField()
        {
            row.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            rows.Add(row.ToArray());
            row = [];
        }

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRow();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            EndRow();
        }

        return rows.Where(r => r.Length > 1 || !string.IsNullOrWhiteSpace(r[0])).ToList();
    }
}
