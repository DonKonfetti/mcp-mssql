-- get_row_count: approximate row count from partition stats.
-- Joins sys.tables, so views yield no row and the caller reports null.
SELECT SUM(ps.row_count) AS row_count
FROM sys.dm_db_partition_stats ps
INNER JOIN sys.tables t
    ON t.object_id = ps.object_id
INNER JOIN sys.schemas s
    ON s.schema_id = t.schema_id
WHERE t.name = @table
    AND (@schema IS NULL OR s.name = @schema)
    AND ps.index_id IN (0, 1);
