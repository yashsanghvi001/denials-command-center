using System.Globalization;
using ClosedXML.Excel;

namespace DenialsCommandCenter.Domain.Worklog;

public static class WorklogReader
{
    public static IReadOnlyList<WorklogRawRow> Read(Stream xlsx)
    {
        using var workbook = new XLWorkbook(xlsx);
        var worksheet = workbook.Worksheets.First();
        var rows = new List<WorklogRawRow>();
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            string CellAt(int column) => CellText(row.Cell(column));
            rows.Add(new WorklogRawRow(row.RowNumber(), CellAt(1), CellAt(2), CellAt(3), CellAt(4), CellAt(5), CellAt(6), CellAt(7), CellAt(8)));
        }
        return rows;
    }

    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => DateOnly.FromDateTime(cell.GetDateTime()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
        _ => cell.GetString(),
    };
}
