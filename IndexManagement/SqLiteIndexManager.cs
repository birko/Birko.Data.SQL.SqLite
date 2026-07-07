using Birko.Data.Patterns.IndexManagement;
using Birko.Data.SQL.Connectors;
using Birko.Data.SQL.IndexManagement;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Data.SQL.SQLite.IndexManagement
{
    /// <summary>
    /// SQLite dialect for <see cref="SqlIndexManager"/>.
    /// Uses sqlite_master and PRAGMA index_info for listing.
    /// </summary>
    public class SqLiteIndexManager : SqlIndexManager
    {
        public SqLiteIndexManager(AbstractConnectorBase connector) : base(connector)
        {
        }

        protected override string IndexExistsSql(string tableName, string indexName)
        {
            var safeIndex = indexName.Replace("'", "''");
            return $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{safeIndex}'";
        }

        protected override string ListIndexesSql(string tableName)
        {
            // Not used — ListAsync is overridden to use PRAGMA. Kept only to satisfy the abstract
            // base contract; the base ListAsync no longer runs for SQLite (CR-H093).
            var safeTable = tableName.Replace("'", "''");
            return $"SELECT name, '', 0, CASE WHEN sql LIKE '%UNIQUE%' THEN 1 ELSE 0 END, 0 FROM sqlite_master WHERE type = 'index' AND tbl_name = '{safeTable}' AND name NOT LIKE 'sqlite_autoindex_%'";
        }

        /// <summary>
        /// SQLite requires PRAGMA index_info per index to get column details. Must be an override
        /// (not `new`) so the inherited GetInfoAsync and IIndexManager/SqlIndexManager-typed callers
        /// dispatch to this PRAGMA implementation instead of the empty-column base query (CR-H093).
        /// </summary>
        public override async Task<IReadOnlyList<IndexInfo>> ListAsync(string? scope = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(scope))
                throw new ArgumentException("Table name (scope) is required for SQL index management.", nameof(scope));

            // Step 1: Get index names
            var indexNames = new List<(string Name, bool IsUnique)>();
            var safeTable = scope!.Replace("'", "''");
            var namesSql = $"SELECT name, CASE WHEN sql LIKE '%UNIQUE%' THEN 1 ELSE 0 END AS is_unique FROM sqlite_master WHERE type = 'index' AND tbl_name = '{safeTable}' AND name NOT LIKE 'sqlite_autoindex_%'";

            await ExecuteReaderAsync(namesSql, reader =>
            {
                while (reader.Read())
                {
                    indexNames.Add((reader.GetString(0), reader.GetInt32(1) != 0));
                }
            }, ct).ConfigureAwait(false);

            // Step 2: For each index, get columns via PRAGMA
            var result = new List<IndexInfo>();
            foreach (var (name, isUnique) in indexNames)
            {
                var safeName = name.Replace("'", "''");
                var fields = new List<IndexField>();

                await ExecuteReaderAsync($"PRAGMA index_info('{safeName}')", reader =>
                {
                    while (reader.Read())
                    {
                        fields.Add(new IndexField
                        {
                            Name = reader.GetString(2), // column name
                            IsDescending = false
                        });
                    }
                }, ct).ConfigureAwait(false);

                result.Add(new IndexInfo
                {
                    Name = name,
                    Unique = isUnique,
                    Fields = fields
                });
            }

            return result;
        }
    }
}
