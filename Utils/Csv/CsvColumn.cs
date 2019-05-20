using System.Collections.Generic;

namespace TslWebApp.Utils.Csv
{
    public class CsvColumn<T> : AbstractColumn<T>
    {
        private string columnHeader = "";
        private List<T> rows;

        public CsvColumn(string columnHeader, List<T> rows)
        {
            this.columnHeader = columnHeader;
            this.rows = rows;
        }

        public override string ColumnHeader { get => columnHeader; set => this.columnHeader = value; }
        public override int Length { get => this.rows.Count; }
        public override List<T> Cells { get => this.rows; set => this.rows = value; }
    }
}
