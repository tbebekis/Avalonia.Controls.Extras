// Copyright (c) 2026 Theodoros Bebekis
// Licensed under the MIT License.

namespace Avalonia.Controls;

/// <summary>
/// Exports group grid data rows to an HTML table.
/// </summary>
public class GroupGridHtmlExporter: GroupGridExporter
{
    // ● private methods
    string Html(string Text)
    {
        return WebUtility.HtmlEncode(Text ?? string.Empty);
    }

    // ● public methods
    /// <inheritdoc />
    public override void Export(GroupGrid Grid, GroupGridExportSnapshot Snapshot, string FilePath)
    {
        Snapshot ??= Grid?.CreateExportSnapshot();
        if (Snapshot == null)
            Snapshot = new GroupGridExportSnapshot(Array.Empty<GroupGridExportColumn>(), Array.Empty<GroupGridExportRow>(), Array.Empty<GroupGridExportCell>());

        StringBuilder Builder = new();
        Builder.AppendLine("<!doctype html>");
        Builder.AppendLine("<html>");
        Builder.AppendLine("<head>");
        Builder.AppendLine("<meta charset=\"utf-8\">");
        Builder.AppendLine("<style>");
        Builder.AppendLine("body{font-family:Arial,sans-serif;font-size:14px}");
        Builder.AppendLine("table{border-collapse:collapse}");
        Builder.AppendLine("th,td{border:1px solid #999;padding:4px 8px;text-align:left}");
        Builder.AppendLine("th{background:#eee}");
        Builder.AppendLine("</style>");
        Builder.AppendLine("</head>");
        Builder.AppendLine("<body>");
        Builder.AppendLine("<table>");
        Builder.AppendLine("<thead>");
        Builder.AppendLine("<tr>");
        foreach (GroupGridExportColumn Column in Snapshot.Columns)
            Builder.Append("<th>").Append(Html(Column.Header)).AppendLine("</th>");
        Builder.AppendLine("</tr>");
        Builder.AppendLine("</thead>");
        Builder.AppendLine("<tbody>");
        foreach (GroupGridExportRow Row in Snapshot.GetDataRows())
        {
            Builder.AppendLine("<tr>");
            foreach (GroupGridExportCell Cell in Row.Cells)
                Builder.Append("<td>").Append(Html(Cell.Text)).AppendLine("</td>");
            Builder.AppendLine("</tr>");
        }
        Builder.AppendLine("</tbody>");
        Builder.AppendLine("</table>");
        Builder.AppendLine("</body>");
        Builder.AppendLine("</html>");

        WriteText(FilePath, Builder.ToString());
    }

    // ● properties
    /// <inheritdoc />
    public override string Name => "HTML";
    /// <inheritdoc />
    public override string DefaultExtension => "html";
}
