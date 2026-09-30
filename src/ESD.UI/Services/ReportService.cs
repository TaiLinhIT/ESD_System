namespace ESD.UI.Services;

public class ReportService
{
    public string BuildCsv(IEnumerable<string[]> rows)
    {
        return string.Join(Environment.NewLine,
            rows.Select(r => string.Join(",", r.Select(Escape))));
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
