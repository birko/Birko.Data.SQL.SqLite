using Birko.Data.SQL.Connectors;
using System;
using System.Collections.Generic;
using System.Text;

namespace Birko.Data.SQL.Repositories
{
    public class SqLiteRepository<TViewModel, TModel> : Data.Repositories.DataBaseRepository<SqLiteConnector, TViewModel, TModel>
        where TModel : Models.AbstractModel, Models.ILoadable<TViewModel>
        where TViewModel : Models.ILoadable<TModel>
    {
        public SqLiteRepository(string path, string filename, InitConnector onInit = null) : base(path, filename, onInit)
        { }
    }
}
