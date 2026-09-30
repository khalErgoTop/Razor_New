using Microsoft.Data.SqlClient;
using System.Globalization;

namespace TOPqwWeb_Core.Legacy.Adapters;

public sealed class QArcAdapter
{
    private static readonly IReadOnlyDictionary<string, string> TablePrefixes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["KV"] = "KV",
            ["BE"] = "BE"
        };

    private readonly string _connectionString;

    public QArcAdapter(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TOPqwWeb")
            ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
    }

    public Task<IReadOnlyList<QArcLayout>> GetLayoutsAsync(string tablePrefix, CancellationToken cancellationToken) =>
        ExecuteLayoutsAsync(GetTablePrefix(tablePrefix), cancellationToken);

    public Task<IReadOnlyList<QArcSheet>> GetSheetsAsync(
        string tablePrefix,
        string eiid,
        int layoutId,
        int status,
        CancellationToken cancellationToken) =>
        ExecuteSheetsAsync(GetTablePrefix(tablePrefix), eiid, layoutId, status, cancellationToken);

    public Task<QArcSheet?> GetSheetAsync(
        string tablePrefix,
        string eiid,
        int layoutId,
        int fkId,
        CancellationToken cancellationToken) =>
        ExecuteSheetAsync(GetTablePrefix(tablePrefix), eiid, layoutId, fkId, cancellationToken);

    public Task<IReadOnlyList<QArcCategory>> GetCategoriesAsync(
        string tablePrefix,
        string eiid,
        int layoutId,
        int fkId,
        CancellationToken cancellationToken) =>
        ExecuteCategoriesAsync(GetTablePrefix(tablePrefix), eiid, layoutId, fkId, cancellationToken);

    public Task<IReadOnlyList<QArcMatrix>> GetMatricesAsync(
        string tablePrefix,
        string eiid,
        int layoutId,
        int fkId,
        CancellationToken cancellationToken) =>
        ExecuteMatricesAsync(GetTablePrefix(tablePrefix), eiid, layoutId, fkId, cancellationToken);

    private async Task<IReadOnlyList<QArcLayout>> ExecuteLayoutsAsync(string tablePrefix, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT LID, Layoutyear, Label, Active
            FROM [{tablePrefix}Layouts]
            WHERE ISNULL(IsDeleted, 0) = 0 AND ISNULL(Active, 0) = 1
            ORDER BY Layoutyear DESC, Label, LID;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<QArcLayout>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                ReadString(reader, 2),
                !reader.IsDBNull(3) && reader.GetBoolean(3)));
        }

        return result;
    }

    private async Task<IReadOnlyList<QArcMatrix>> ExecuteMatricesAsync(
        string tablePrefix,
        string eiid,
        int layoutId,
        int fkId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            DECLARE @sid int;
            SELECT @sid = SID FROM [{tablePrefix}Sheets] WHERE FKEIID = @eiid AND FKLID = @layoutId AND FKID = @fkId;
            SELECT t.TID, t.Label, x.XID, x.Text, x.Position, x.DataType,
                   y.YID, y.Text AS RowText, y.Position AS RowPosition, CAST(0 AS bit) AS IsExpanded,
                   v.WertINT, v.WertDEC, v.WertBool, v.WertTXT, v.WertMEMO
            FROM [{tablePrefix}Terms] t
            INNER JOIN [{tablePrefix}MatrixX] x ON x.FKTID = t.TID
            INNER JOIN [{tablePrefix}MatrixY] y ON y.FKTID = t.TID
            LEFT JOIN [{tablePrefix}IndexCategoryTerms] ict ON ict.FKTID = t.TID
            LEFT JOIN [{tablePrefix}Values] v ON v.FKICT = ict.ICT
                AND v.FKXID = x.XID
                AND v.FKYID = y.YID
                AND v.FKSID = @sid
            WHERE t.Format = 'dbMatrix' AND ISNULL(t.IsDeleted, 0) = 0
            UNION ALL
            SELECT t.TID, t.Label, x.XID, x.Text, x.Position, x.DataType,
                   ey.YEID, ey.Text AS RowText, ey.Position AS RowPosition, CAST(1 AS bit) AS IsExpanded,
                   ev.WertINT, ev.WertDEC, ev.WertBool, ev.WertTXT, ev.WertMEMO
            FROM [{tablePrefix}Terms] t
            INNER JOIN [{tablePrefix}MatrixX] x ON x.FKTID = t.TID
            INNER JOIN [{tablePrefix}MatrixYExpanded] ey ON ey.FKTID = t.TID AND ey.FKSID = @sid
            LEFT JOIN [{tablePrefix}MatrixYExpandedValues] ev ON ev.FKYID = ey.YEID AND ev.FKXID = x.XID
            WHERE t.Format = 'dbMatrix' AND ISNULL(t.IsDeleted, 0) = 0
            ORDER BY TID, Position, IsExpanded;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eiid", eiid);
        command.Parameters.AddWithValue("@layoutId", layoutId);
        command.Parameters.AddWithValue("@fkId", fkId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var matrices = new Dictionary<int, QArcMatrixBuilder>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var termId = reader.GetInt32(0);
            if (!matrices.TryGetValue(termId, out var matrix))
            {
                matrix = new QArcMatrixBuilder(termId, ReadString(reader, 1));
                matrices.Add(termId, matrix);
            }

            var columnId = reader.GetInt32(2);
            var rowId = reader.GetInt32(6);
            matrix.Columns.TryAdd(columnId, new QArcMatrixColumn(columnId, ReadString(reader, 3), reader.IsDBNull(4) ? 0 : reader.GetInt32(4), ReadString(reader, 5)));
            matrix.Rows.TryAdd(rowId, new QArcMatrixRow(rowId, ReadString(reader, 7), reader.IsDBNull(8) ? 0 : reader.GetInt32(8), reader.GetBoolean(9)));
            matrix.Values[(rowId, columnId)] = ReadValue(reader, 10);
        }

        return matrices.Values.Select(matrix => matrix.ToRecord()).ToArray();
    }

    private async Task<IReadOnlyList<QArcSheet>> ExecuteSheetsAsync(
        string tablePrefix,
        string eiid,
        int layoutId,
        int status,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT SID, FKEIID, FKLID, FKID, Label, Status
            FROM [{tablePrefix}Sheets]
            WHERE FKEIID = @eiid AND FKLID = @layoutId
              AND (@status = 0 OR Status = @status)
            ORDER BY Label, SID;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eiid", eiid);
        command.Parameters.AddWithValue("@layoutId", layoutId);
        command.Parameters.AddWithValue("@status", status);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<QArcSheet>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadSheet(reader));
        }

        return result;
    }

    private async Task<QArcSheet?> ExecuteSheetAsync(
        string tablePrefix,
        string eiid,
        int layoutId,
        int fkId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT SID, FKEIID, FKLID, FKID, Label, Status
            FROM [{tablePrefix}Sheets]
            WHERE FKEIID = @eiid AND FKLID = @layoutId AND FKID = @fkId;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eiid", eiid);
        command.Parameters.AddWithValue("@layoutId", layoutId);
        command.Parameters.AddWithValue("@fkId", fkId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSheet(reader) : null;
    }

    private async Task<IReadOnlyList<QArcCategory>> ExecuteCategoriesAsync(
        string tablePrefix,
        string eiid,
        int layoutId,
        int fkId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            DECLARE @sid int;
            SELECT @sid = SID FROM [{tablePrefix}Sheets]
            WHERE FKEIID = @eiid AND FKLID = @layoutId AND FKID = @fkId;

            SELECT ilc.ILC, ilc.ParentID, ilc.Position, c.CID, c.Label, c.AdditionalText,
                   ict.ICT, ict.Position AS TermPosition, t.TID, t.Label AS TermLabel,
                   t.AdditionalText AS TermAdditionalText, t.DesignType, t.Format,
                   v.WertINT, v.WertDEC, v.WertBool, v.WertTXT, v.WertMEMO,
                   rv.WertINT, rv.WertDEC, rv.WertBool, rv.WertTXT, rv.WertMEMO,
                   qv.WertINT, qv.WertDEC, qv.WertBool, qv.WertTXT, qv.WertMEMO
            FROM [{tablePrefix}IndexLayoutCategories] ilc
            INNER JOIN [{tablePrefix}Categories] c ON c.CID = ilc.FKCID
            LEFT JOIN [{tablePrefix}IndexCategoryTerms] ict ON ict.FKILC = ilc.ILC
            LEFT JOIN [{tablePrefix}Terms] t ON t.TID = ict.FKTID
            OUTER APPLY (
                SELECT TOP 1 WertINT, WertDEC, WertBool, WertTXT, WertMEMO
                FROM [{tablePrefix}Values]
                WHERE FKICT = ict.ICT AND FKSID = @sid
            ) v
            OUTER APPLY (
                SELECT TOP 1 WertINT, WertDEC, WertBool, WertTXT, WertMEMO
                FROM [{tablePrefix}ValuesRevision]
                WHERE FKICT = ict.ICT AND FKSID = @sid
            ) rv
            OUTER APPLY (
                SELECT TOP 1 WertINT, WertDEC, WertBool, WertTXT, WertMEMO
                FROM [{tablePrefix}ValuesQuota]
                WHERE FKICT = ict.ICT AND FKSID = @sid
            ) qv
            WHERE ilc.FKLID = @layoutId
              AND ISNULL(ilc.IsDeleted, 0) = 0
              AND ISNULL(c.IsDeleted, 0) = 0
              AND (ict.ICT IS NULL OR ISNULL(ict.IsDeleted, 0) = 0)
              AND (t.TID IS NULL OR ISNULL(t.IsDeleted, 0) = 0)
            ORDER BY ilc.ParentID, ilc.Position, ict.Position, ict.ICT;
            """;

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@eiid", eiid);
        command.Parameters.AddWithValue("@layoutId", layoutId);
        command.Parameters.AddWithValue("@fkId", fkId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var categories = new Dictionary<int, QArcCategoryBuilder>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var categoryId = reader.GetInt32(3);
            if (!categories.TryGetValue(categoryId, out var category))
            {
                category = new QArcCategoryBuilder(
                    categoryId,
                    reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    ReadString(reader, 4),
                    ReadString(reader, 5));
                categories.Add(categoryId, category);
            }

            if (!reader.IsDBNull(8))
            {
                category.Terms.Add(new QArcTerm(
                    reader.GetInt32(8),
                    reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    ReadString(reader, 9),
                    ReadString(reader, 10),
                    ReadString(reader, 11),
                    ReadString(reader, 12),
                    ReadValue(reader, 13),
                    ReadValue(reader, 18),
                    ReadValue(reader, 23)));
            }
        }

        return categories.Values
            .OrderBy(category => category.Position)
            .Select(category => category.ToRecord())
            .ToArray();
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string GetTablePrefix(string tablePrefix)
    {
        if (TablePrefixes.TryGetValue(tablePrefix, out var safePrefix)) return safePrefix;
        throw new ArgumentException("Der QArc-Tabellenpräfix ist nicht zulässig.", nameof(tablePrefix));
    }

    private static QArcSheet ReadSheet(SqlDataReader reader) => new(
        reader.GetInt32(0),
        ReadString(reader, 1),
        reader.GetInt32(2),
        reader.GetInt32(3),
        ReadString(reader, 4),
        reader.IsDBNull(5) ? 0 : reader.GetInt32(5));

    private static string? ReadValue(SqlDataReader reader, int startIndex = 14)
    {
        for (var index = startIndex; index < startIndex + 5; index++)
        {
            if (!reader.IsDBNull(index)) return Convert.ToString(reader.GetValue(index));
        }

        return null;
    }

    private static string ReadString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
}

public sealed record QArcLayout(int Id, int? Year, string Label, bool Active);

public sealed record QArcSheet(int Id, string EIID, int LayoutId, int ForeignKeyId, string Label, int Status);

public sealed record QArcCategory(int Id, int ParentId, int Position, string Label, string AdditionalText, IReadOnlyList<QArcTerm> Terms);

public sealed record QArcTerm(
    int Id,
    int Position,
    string Label,
    string AdditionalText,
    string DesignType,
    string Format,
    string? Value,
    string? RevisionValue = null,
    string? QuotaValue = null);

public sealed record QArcMatrix(int TermId, string Label, IReadOnlyList<QArcMatrixColumn> Columns, IReadOnlyList<QArcMatrixRow> Rows, IReadOnlyDictionary<(int RowId, int ColumnId), string?> Values);

public sealed record QArcMatrixColumn(int Id, string Label, int Position, string DataType);

public sealed record QArcMatrixRow(int Id, string Label, int Position, bool IsExpanded);

internal sealed class QArcCategoryBuilder(int id, int parentId, int position, string label, string additionalText)
{
    public int Id { get; } = id;
    public int ParentId { get; } = parentId;
    public int Position { get; } = position;
    public string Label { get; } = label;
    public string AdditionalText { get; } = additionalText;
    public List<QArcTerm> Terms { get; } = [];

    public QArcCategory ToRecord() => new(Id, ParentId, Position, Label, AdditionalText, Terms);
}

internal sealed class QArcMatrixBuilder(int termId, string label)
{
    public int TermId { get; } = termId;
    public string Label { get; } = label;
    public Dictionary<int, QArcMatrixColumn> Columns { get; } = [];
    public Dictionary<int, QArcMatrixRow> Rows { get; } = [];
    public Dictionary<(int RowId, int ColumnId), string?> Values { get; } = [];

    public QArcMatrix ToRecord() => new(
        TermId,
        Label,
        Columns.Values.OrderBy(column => column.Position).ToArray(),
        Rows.Values.OrderBy(row => row.Position).ToArray(),
        Values);
}
