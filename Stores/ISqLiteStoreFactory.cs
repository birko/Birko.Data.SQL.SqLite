using Birko.Data.Models;
using Birko.Data.SQL.Connectors;

namespace Birko.Data.SQL.SqLite.Stores
{
    /// <summary>
    /// Creates configured SQLite stores over one shared <see cref="SqLiteSettings"/>, so callers never
    /// construct settings themselves. The underlying connector is cached by Birko (keyed on the
    /// settings id), so creating a fresh store per call is cheap.
    /// </summary>
    public interface ISqLiteStoreFactory
    {
        /// <summary>The shared settings all stores from this factory use.</summary>
        SqLiteSettings Settings { get; }

        /// <summary>Returns a store for <typeparamref name="T"/> wired to the configured database.</summary>
        SQLiteStore<T> GetStore<T>() where T : AbstractModel;

        /// <summary>
        /// The shared connector for the configured database — the same cached instance the stores use.
        /// Useful for the migration runner, which needs the connector to provision the schema.
        /// </summary>
        AbstractConnector GetConnector();
    }
}
