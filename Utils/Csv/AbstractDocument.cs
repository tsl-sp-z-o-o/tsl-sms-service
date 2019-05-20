using System.Collections.Generic;

namespace TslWebApp.Utils.Csv
{
    public abstract class AbstractDocument<T>
    {
        public abstract string Title { get; set; }
        public abstract List<T> Cols {get; set;}
    }
}
