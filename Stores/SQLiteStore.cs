using Birko.Data.SQL.Connectors;
using Birko.Data.SQL.Stores;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Birko.Data.SQL.SqLite.Stores
{
    /// <summary>
    /// SQLite store with native bulk operation support.
    /// Combines single-item and bulk CRUD operations in one store.
    /// </summary>
    /// <typeparam name="T">The type of entity.</typeparam>
    public class SQLiteStore<T> : DataBaseBulkStore<SqLiteConnector, T>
        where T : Data.Models.AbstractModel
    {
        /// <summary>
        /// Initializes a new instance of the SQLiteStore class.
        /// </summary>
        public SQLiteStore()
        {
        }

        /// <summary>
        /// Sets the connection settings.
        /// </summary>
        /// <param name="settings">The password settings to use.</param>
        public override void SetSettings(Birko.Configuration.PasswordSettings settings)
        {
            if (settings != null)
            {
                base.SetSettings(settings);
            }
        }

        #region Native Bulk Operations

        /// <inheritdoc />
        public override void Create(IEnumerable<T> data, Data.Stores.StoreDataDelegate<T>? storeDelegate = null)
        {
            if (Connector == null || data == null || !data.Any())
                return;

            var items = data.ToList();
            foreach (var item in items)
            {
                item.Guid = Guid.NewGuid();
                storeDelegate?.Invoke(item);
            }

            Connector.BulkInsert(typeof(T), items.Cast<object>());
        }

        /// <inheritdoc />
        public override void Update(IEnumerable<T> data, Data.Stores.StoreDataDelegate<T>? storeDelegate = null)
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

            Connector.BulkUpdate(typeof(T), items.Cast<object>());
        }

        /// <inheritdoc />
        public override void Delete(IEnumerable<T> data)
        {
            if (Connector == null || data == null || !data.Any())
                return;

            Connector.BulkDelete(typeof(T), data.Cast<object>());
        }

        #endregion
    }
}
