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
    /// Async SQLite repository with native async database operations and bulk support.
    /// Uses AsyncSQLiteStore which provides bulk operations via transactions.
    /// </summary>
    /// <typeparam name="TViewModel">The type of view model.</typeparam>
    /// <typeparam name="TModel">The type of data model.</typeparam>
    public class AsyncSqLiteRepository<TViewModel, TModel>
        : Data.Repositories.AbstractAsyncBulkViewModelRepository<TViewModel, TModel>
        where TModel : Models.AbstractModel, Models.ILoadable<TViewModel>
        where TViewModel : Models.ILoadable<TModel>
    {
        /// <summary>
        /// Gets the SQLite connector.
        /// This works with wrapped stores (e.g., tenant wrappers).
        /// </summary>
        public SqLiteConnector? Connector => Store?.GetUnwrappedStore<TModel, AsyncSQLiteStore<TModel>>()?.Connector;

        /// <summary>
        /// The database file path.
        /// </summary>
        public string? Path => Connector?.Path;

        /// <summary>
        /// Initializes a new instance of the AsyncSqLiteRepository class.
        /// </summary>
        public AsyncSqLiteRepository()
            : base(null)
        {
            Store = new AsyncSQLiteStore<TModel>();
        }

        /// <summary>
        /// Initializes a new instance with dependency injection support.
        /// </summary>
        /// <param name="store">The async SQLite store to use (optional). Can be wrapped (e.g., by tenant wrappers).</param>
        public AsyncSqLiteRepository(Data.Stores.IAsyncStore<TModel>? store)
            : base(null)
        {
            if (store != null && !store.IsStoreOfType<TModel, AsyncSQLiteStore<TModel>>())
            {
                throw new ArgumentException(
                    "Store must be of type AsyncSQLiteStore<TModel> or a wrapper around it (e.g., AsyncTenantStoreWrapper).",
                    nameof(store));
            }
            Store = store ?? new AsyncSQLiteStore<TModel>();
        }

        /// <summary>
        /// Sets the connection settings.
        /// </summary>
        /// <param name="settings">The password settings to use.</param>
        public void SetSettings(PasswordSettings settings)
        {
            if (settings != null)
            {
                var innerStore = Store?.GetUnwrappedStore<TModel, AsyncSQLiteStore<TModel>>();
                innerStore?.SetSettings(settings);
            }
        }

        /// <summary>
        /// Initializes the repository and creates the database schema if needed.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task InitAsync(CancellationToken ct = default)
        {
            if (Connector == null)
            {
                throw new InvalidOperationException("Connector not initialized. Call SetSettings() first.");
            }

            await Task.Run(() => Connector.DoInit(), ct).ConfigureAwait(false);
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

            await Task.Run(() => Connector.DropTable(new[] { typeof(TModel) }), ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates the database schema for the model type.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task CreateSchemaAsync(CancellationToken ct = default)
        {
            if (Connector == null)
            {
                throw new InvalidOperationException("Connector not initialized.");
            }

            await Task.Run(() => Connector.CreateTable(new[] { typeof(TModel) }), ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if the database file exists.
        /// </summary>
        public bool DatabaseExists()
        {
            return !string.IsNullOrEmpty(Path) && System.IO.File.Exists(Path);
        }

        /// <summary>
        /// Gets the size of the database file in bytes.
        /// </summary>
        public long GetDatabaseSize()
        {
            if (DatabaseExists())
            {
                var fileInfo = new System.IO.FileInfo(Path!);
                return fileInfo.Length;
            }
            return 0;
        }

        /// <inheritdoc />
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
