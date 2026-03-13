using Birko.Data.SQL.Connectors;
using Birko.Data.Stores;
using Birko.Data.SQL.SqLite.Stores;
using Birko.Data.SQL.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;
using PasswordSettings = Birko.Data.Stores.PasswordSettings;

namespace Birko.Data.SQL.Repositories
{
    /// <summary>
    /// Async SQLite repository for direct model access with bulk support.
    /// </summary>
    /// <typeparam name="T">The type of data model.</typeparam>
    public class AsyncSqLiteModelRepository<T>
        : Data.Repositories.AbstractAsyncBulkRepository<T>
        where T : Models.AbstractModel
    {
        /// <summary>
        /// Gets the SQLite connector.
        /// </summary>
        public SqLiteConnector? Connector => Store?.GetUnwrappedStore<T, AsyncSQLiteStore<T>>()?.Connector;

        /// <summary>
        /// The database file path.
        /// </summary>
        public string? Path => Connector?.Path;

        public AsyncSqLiteModelRepository()
            : base(null)
        {
            Store = new AsyncSQLiteStore<T>();
        }

        public AsyncSqLiteModelRepository(Data.Stores.IAsyncStore<T>? store)
            : base(null)
        {
            if (store != null && !store.IsStoreOfType<T, AsyncSQLiteStore<T>>())
            {
                throw new ArgumentException(
                    "Store must be of type AsyncSQLiteStore<T> or a wrapper around it.",
                    nameof(store));
            }
            Store = store ?? new AsyncSQLiteStore<T>();
        }

        public void SetSettings(PasswordSettings settings)
        {
            if (settings != null)
            {
                var innerStore = Store?.GetUnwrappedStore<T, AsyncSQLiteStore<T>>();
                innerStore?.SetSettings(settings);
            }
        }

        public async Task InitAsync(CancellationToken ct = default)
        {
            if (Connector == null)
                throw new InvalidOperationException("Connector not initialized. Call SetSettings() first.");
            await Task.Run(() => Connector.DoInit(), ct).ConfigureAwait(false);
        }

        public async Task DropAsync(CancellationToken ct = default)
        {
            if (Connector == null)
                throw new InvalidOperationException("Connector not initialized.");
            await Task.Run(() => Connector.DropTable(new[] { typeof(T) }), ct).ConfigureAwait(false);
        }

        public async Task CreateSchemaAsync(CancellationToken ct = default)
        {
            if (Connector == null)
                throw new InvalidOperationException("Connector not initialized.");
            await Task.Run(() => Connector.CreateTable(new[] { typeof(T) }), ct).ConfigureAwait(false);
        }

        public bool DatabaseExists()
        {
            return !string.IsNullOrEmpty(Path) && System.IO.File.Exists(Path);
        }

        public long GetDatabaseSize()
        {
            if (DatabaseExists())
            {
                var fileInfo = new System.IO.FileInfo(Path!);
                return fileInfo.Length;
            }
            return 0;
        }

        public override async Task DestroyAsync(CancellationToken ct = default)
        {
            await base.DestroyAsync(ct);
            if (Connector != null)
            {
                await DropAsync(ct);
            }
        }
    }
}
