using Birko.Data.SQL.Connector;
using System;
using System.Collections.Generic;
using System.Text;

namespace Birko.Data.SQL.Repository
{
    public class SqLiteRepository<TViewModel, TModel> : Data.Repository.DataBaseRepository<SqLiteConnector, TViewModel, TModel>
        where TModel : Model.AbstractModel, Model.ILoadable<TViewModel>
        where TViewModel : Model.ILoadable<TModel>
    {
        public SqLiteRepository(string path, string filename, InitConnector onInit = null) : base(path, filename, onInit)
        { }
    }
}
