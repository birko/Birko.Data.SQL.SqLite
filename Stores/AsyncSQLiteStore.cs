using Birko.Data.SQL.Connectors;
using Birko.Data.SQL.Stores;
using Birko.Data.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Data.SQL.SqLite.Stores
{
    /// <summary>
    /// Native async SQLite store with bulk operation support.
    /// Combines single-item and bulk async CRUD operations in one store.
    /// </summary>
    /// <typeparam name="T">The type of entity.</typeparam>
    public class AsyncSQLiteStore<T> : AsyncDataBaseBulkStore<SqLiteConnector, T>
        where T : Models.AbstractModel
    {
        /// <summary>
        /// Initializes a new instance of the AsyncSQLiteStore class.
        /// </summary>
        public AsyncSQLiteStore()
        {
        }

        /// <summary>
        /// Sets the connection settings.
        /// </summary>
        /// <param name="settings">The password settings to use.</param>
        public override void SetSettings(PasswordSettings settings)
        {
            if (settings != null)
            {
                base.SetSettings(settings);
            }
        }

        /// <summary>
        /// Creates the database schema.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task CreateSchemaAsync(CancellationToken ct = default)
        {
            if (Connector == null)
            {
                throw new InvalidOperationException("Connector not initialized. Call SetSettings() first.");
            }

            await Task.Run(() => Connector.CreateTable(new[] { typeof(T) }), ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Drops the database schema.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task DropAsync(CancellationToken ct = default)
        {
            if (Connector == null)
            {
                throw new InvalidOperationException("Connector not initialized.");
            }

            await Task.Run(() => Connector.DropTable(new[] { typeof(T) }), ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if the database file exists.
        /// </summary>
        /// <returns>True if the database file exists, false otherwise.</returns>
        public bool DatabaseExists()
        {
            return !string.IsNullOrEmpty(Path) && System.IO.File.Exists(Path);
        }

        /// <summary>
        /// Gets the size of the database file in bytes.
        /// </summary>
        /// <returns>The file size in bytes, or 0 if the file doesn't exist.</returns>
        public long GetDatabaseSize()
        {
            if (DatabaseExists())
            {
                var fileInfo = new System.IO.FileInfo(Path!);
                return fileInfo.Length;
            }
            return 0;
        }

        /// <summary>
        /// Gets the database file path.
        /// </summary>
        public string? Path => Connector?.Path;

        #region Native Bulk Operations

        /// <inheritdoc />
        public override async Task CreateAsync(
            IEnumerable<T> data,
            StoreDataDelegate<T>? storeDelegate = null,
            CancellationToken ct = default)
        {
            if (Connector == null || data == null || !data.Any())
                return;

            var items = data.ToList();
            foreach (var item in items)
            {
                item.Guid = Guid.NewGuid();
                storeDelegate?.Invoke(item);
            }

            await Connector.BulkInsertAsync(typeof(T), items.Cast<object>(), ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task UpdateAsync(
            IEnumerable<T> data,
            StoreDataDelegate<T>? storeDelegate = null,
            CancellationToken ct = default)
        {
            if (Connector == null || data == null || !data.Any())
                return;

            var items = data.ToList();
            if (storeDelegate != null)
            {
                foreach (var item in items)
                {
                    storeDelegate.Invoke(item);
                }
            }

            await Connector.BulkUpdateAsync(typeof(T), items.Cast<object>(), ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task DeleteAsync(
            IEnumerable<T> data,
            CancellationToken ct = default)
        {
            if (Connector == null || data == null || !data.Any())
                return;

            await Connector.BulkDeleteAsync(typeof(T), data.Cast<object>(), ct).ConfigureAwait(false);
        }

        #endregion
    }
}
