namespace Birko.Data.SQL.Repositories
{
    /// <summary>
    /// SQLite repository for direct model access with bulk support.
    /// </summary>
    /// <typeparam name="T">The type of data model.</typeparam>
    public class SqLiteModelRepository<T>
        : DataBaseModelRepository<SQL.Connectors.SqLiteConnector, T>
        where T : Models.AbstractModel
    {
        public SqLiteModelRepository() : base()
        { }
    }
}
