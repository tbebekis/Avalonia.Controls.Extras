// Copyright (c) 2026 Theodoros Bebekis
// Licensed under the MIT License.

namespace Avalonia.Controls.Extras.Grid.Tests;

/// <summary>
/// Tests built-in group grid exporters.
/// </summary>
public class GroupGridExporterTests
{
    // ● private methods
    GroupGridExportSnapshot CreateSnapshot()
    {
        GroupGridExportColumn NameColumn = new(new GroupGridTextColumn { Name = "Name", Header = "Name" });
        GroupGridExportColumn AmountColumn = new(new GroupGridNumberColumn { Name = "Amount", Header = "Amount" });
        GroupGridExportRow Row = new(
            new GroupGridRowInfo(GroupGridRowKind.DataRow, 0, 0, 0, null, null, null, false),
            string.Empty,
            new[]
            {
                new GroupGridExportCell(NameColumn, "Alpha, \"One\"", "Alpha, \"One\""),
                new GroupGridExportCell(AmountColumn, 12.5m, "12.5"),
            });

        return new GroupGridExportSnapshot(new[] { NameColumn, AmountColumn }, new[] { Row }, Array.Empty<GroupGridExportCell>());
    }
    string TempPath(string Extension)
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "." + Extension);
    }

    // ● tests
    /// <summary>
    /// Verifies CSV escaping for delimiters and quotes.
    /// </summary>
    [Fact]
    public void CsvExporter_WithSpecialText_EscapesCsvCells()
    {
        string FilePath = TempPath("csv");
        try
        {
            new GroupGridCsvExporter().Export(null, CreateSnapshot(), FilePath);
            string Text = File.ReadAllText(FilePath);

            Assert.Contains("Name,Amount", Text);
            Assert.Contains("\"Alpha, \"\"One\"\"\",12.5", Text);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Verifies formatted JSON export with column metadata and values.
    /// </summary>
    [Fact]
    public void JsonExporter_WithSnapshot_WritesFormattedJson()
    {
        string FilePath = TempPath("json");
        try
        {
            new GroupGridJsonExporter().Export(null, CreateSnapshot(), FilePath);
            string Text = File.ReadAllText(FilePath);
            using JsonDocument Document = JsonDocument.Parse(Text);

            Assert.Contains(Environment.NewLine, Text);
            Assert.Equal("Name", Document.RootElement.GetProperty("Columns")[0].GetProperty("Name").GetString());
            Assert.Equal("Alpha, \"One\"", Document.RootElement.GetProperty("Rows")[0].GetProperty("Name").GetString());
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Verifies HTML escaping.
    /// </summary>
    [Fact]
    public void HtmlExporter_WithSpecialText_EncodesHtmlCells()
    {
        string FilePath = TempPath("html");
        try
        {
            new GroupGridHtmlExporter().Export(null, CreateSnapshot(), FilePath);
            string Text = File.ReadAllText(FilePath);

            Assert.Contains("<table>", Text);
            Assert.Contains("Alpha, &quot;One&quot;", Text);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
