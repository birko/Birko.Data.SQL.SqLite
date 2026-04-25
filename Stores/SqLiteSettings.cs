using System;
using Birko.Configuration;
using Birko.Data.Models;

namespace Birko.Data.SQL.SqLite.Stores
{
    /// <summary>
    /// SQLite-specific settings.
    /// Extends PasswordSettings (not RemoteSettings/SqlSettings) since SQLite is file-based
    /// and doesn't need username, port, or secure connection options.
    /// </summary>
    public class SqLiteSettings : PasswordSettings, ILoadable<SqLiteSettings>
    {
        /// <summary>
        /// Gets or sets the command timeout in seconds. Default is 30.
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        public SqLiteSettings() : base() { }

        public SqLiteSettings(string location, string name, string? password = null)
            : base(location, name, password ?? string.Empty) { }

        /// <summary>
        /// Gets the database file path derived from Location and Name.
        /// </summary>
        public string? Path => (!string.IsNullOrEmpty(Location) && !string.IsNullOrEmpty(Name))
            ? System.IO.Path.Combine(Location, Name)
            : null;

        /// <summary>
        /// Gets the SQLite connection string from the current settings.
        /// </summary>
        public virtual string GetConnectionString()
        {
            var cs = $"Data Source={Path}";
            if (!string.IsNullOrEmpty(Password))
            {
                cs += $";Password={Password}";
            }
            cs += $";Default Timeout={CommandTimeout}";
            return cs;
        }

        public void LoadFrom(SqLiteSettings data)
        {
            if (data != null)
            {
                base.LoadFrom((PasswordSettings)data);
                CommandTimeout = data.CommandTimeout;
            }
        }

        public override void LoadFrom(Birko.Configuration.Settings data)
        {
            if (data is SqLiteSettings sqliteData)
            {
                LoadFrom(sqliteData);
            }
            else
            {
                base.LoadFrom(data);
            }
        }
    }
}
