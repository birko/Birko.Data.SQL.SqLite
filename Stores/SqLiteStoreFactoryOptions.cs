namespace Birko.Data.SQL.SqLite.Stores
{
    /// <summary>
    /// Configuration for <see cref="SqLiteStoreFactory"/>: where the database file lives and its
    /// connection tuning. Kept free of any ASP.NET/hosting types — a relative <see cref="Location"/>
    /// is resolved against the caller-supplied <see cref="BaseDirectory"/> (e.g. a host content root),
    /// so the SQLite layer never takes a dependency on <c>IHostEnvironment</c>.
    /// </summary>
    public class SqLiteStoreFactoryOptions
    {
        /// <summary>
        /// Directory that holds the database file. If relative and <see cref="BaseDirectory"/> is set,
        /// it is resolved against <see cref="BaseDirectory"/>; otherwise used as-is.
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>The database file name (e.g. <c>app.db</c>).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional database password.</summary>
        public string? Password { get; set; }

        /// <summary>Command timeout in seconds. Default is 30.</summary>
        public int CommandTimeout { get; set; } = 30;

        /// <summary>
        /// Optional base directory that a relative <see cref="Location"/> is resolved against
        /// (typically the host content root). Ignored when <see cref="Location"/> is already rooted.
        /// </summary>
        public string? BaseDirectory { get; set; }
    }
}
