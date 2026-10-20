using System.Text;

namespace RevUpIQ.Admin.Infrastructure;

// Reads CSV text as saved by Excel, Google Sheets or Numbers: quoted fields may hold
// commas, line breaks and doubled quotes (""). Comma, semicolon (European Excel) or
// tab separated, whichever the header line uses most. Each row keeps its place, so
// row index + 1 is the spreadsheet row number; blank rows come back as empty fields.
public static class CsvTable
{
    public static List<string[]> Parse(string text)
    {
        text = text.TrimStart('﻿');
        var delimiter = DetectDelimiter(text);

        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c != '"')
                    field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                    inQuotes = false;
            }
            else if (c == '"' && field.Length == 0)
                inQuotes = true;
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.ToArray());
                row.Clear();
            }
            else
                field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        var end = text.IndexOfAny(new[] { '\r', '\n' });
        var header = end < 0 ? text : text[..end];
        return new[] { ',', ';', '\t' }.OrderByDescending(d => header.Count(ch => ch == d)).First();
    }
}
