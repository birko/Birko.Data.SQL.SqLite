using System;
using System.IO;
using Birko.Data.Models;
using Birko.Data.SQL.Connectors;

namespace Birko.Data.SQL.SqLite.Stores
{
    /// <summary>
    /// Default <see cref="ISqLiteStoreFactory"/>: builds one shared <see cref="SqLiteSettings"/>
    /// (database directory resolved and created on construction) and hands out <see cref="SQLiteStore{T}"/>
    /// instances. Removes the boilerplate every SQLite host otherwise repeats — resolving the DB path,
    /// creating the parent directory (SQLite creates the file on demand but not its folder), and
    /// exposing the shared connector for a migrator.
    /// </summary>
    public sealed class SqLiteStoreFactory : ISqLiteStoreFactory
    {
        /// <inheritdoc />
        public SqLiteSettings Settings { get; }

        /// <summary>Builds the factory from <paramref name="options"/>, creating the DB directory if missing.</summary>
        public SqLiteStoreFactory(SqLiteStoreFactoryOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var location = options.Location;
            if (!Path.IsPathRooted(location) && !string.IsNullOrEmpty(options.BaseDirectory))
            {
                location = Path.Combine(options.BaseDirectory, location);
            }

            if (!string.IsNullOrEmpty(location))
            {
                // SQLite creates the database file on demand, but not its parent directory.
                Directory.CreateDirectory(location);
            }

            Settings = new SqLiteSettings(location, options.Name, options.Password)
            {
                CommandTimeout = options.CommandTimeout,
            };
        }

        /// <inheritdoc />
        public SQLiteStore<T> GetStore<T>() where T : AbstractModel
        {
            var store = new SQLiteStore<T>();
            store.SetSettings(Settings);
            return store;
        }

        /// <inheritdoc />
        public AbstractConnector GetConnector() => DataBase.GetConnector<SqLiteConnector>(Settings);
    }
}
