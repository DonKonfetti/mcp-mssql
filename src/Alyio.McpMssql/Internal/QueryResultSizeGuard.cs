// MIT License

using System.Globalization;
using System.Text;
using Alyio.McpMssql.Models;

namespace Alyio.McpMssql.Internal;

internal static class QueryResultSizeGuard
{
    public static long AddValue(
        long currentByteCount,
        object? value,
        int resultByteLimit,
        int cellByteLimit)
    {
        int valueBytes = GetValueByteCount(value);
        if (valueBytes > cellByteLimit)
        {
            throw new InvalidOperationException(
                $"A query result value exceeded the {cellByteLimit}-byte safety limit.");
        }

        long totalBytes = currentByteCount + valueBytes;
        if (totalBytes > resultByteLimit)
        {
            throw new InvalidOperationException(
                $"The query result exceeded the {resultByteLimit}-byte safety limit. " +
                "Select fewer rows or columns.");
        }

        return totalBytes;
    }

    public static string SerializeCsv(SelectResult result, int resultByteLimit)
    {
        var csv = CsvSerializer.Serialize(result.Columns, result.Rows);
        if (Encoding.UTF8.GetByteCount(csv) > resultByteLimit)
        {
            throw new InvalidOperationException(
                $"The serialized query result exceeded the {resultByteLimit}-byte safety limit. " +
                "Select fewer rows or columns.");
        }

        return csv;
    }

    private static int GetValueByteCount(object? value)
    {
        return value switch
        {
            null => 0,
            string text => Encoding.UTF8.GetByteCount(text),
            byte[] bytes => bytes.Length,
            char[] characters => Encoding.UTF8.GetByteCount(characters),
            _ => Encoding.UTF8.GetByteCount(
                Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
        };
    }
}
