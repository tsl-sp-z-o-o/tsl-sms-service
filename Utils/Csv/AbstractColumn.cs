using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TslWebApp.Utils.Csv;

namespace TslWebApp.Utils
{
    public abstract class AbstractColumn<T>
    {
        public abstract string ColumnHeader { get; set; }
        public abstract int Length { get; }
        public abstract List<T> Cells {get; set;}
    }
}
