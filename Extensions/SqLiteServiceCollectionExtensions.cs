using System;
using Birko.Data.SQL.SqLite.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Birko.Data.SQL.SqLite
{
    /// <summary>
    /// DI helpers for wiring the SQLite store factory.
    /// </summary>
    public static class SqLiteServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a singleton <see cref="ISqLiteStoreFactory"/> configured by <paramref name="configure"/>.
        /// The database directory is resolved and created eagerly (so a bad path fails at startup, not on
        /// first query). Resolve <see cref="ISqLiteStoreFactory"/> to get stores / the shared connector.
        /// </summary>
        public static IServiceCollection AddSqLiteStores(
            this IServiceCollection services,
            Action<SqLiteStoreFactoryOptions> configure)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new SqLiteStoreFactoryOptions();
            configure(options);

            var factory = new SqLiteStoreFactory(options);
            services.AddSingleton<ISqLiteStoreFactory>(factory);
            return services;
        }
    }
}
