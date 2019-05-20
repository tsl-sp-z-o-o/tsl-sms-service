using System.Collections.Generic;


namespace TslWebApp.Utils.Csv
{
    public class CsvDocument : AbstractDocument<CsvColumn<CsvColCell<string>>>
    {
        private string title;
        private List<CsvColumn<CsvColCell<string>>> cols;

        public CsvDocument(){}

        public CsvDocument(string title, List<CsvColCell<string>> rows, List<CsvColumn<CsvColCell<string>>> cols)
        {
            this.title = title;
            this.cols = cols;
        }

        public override string Title { get => title; set => this.title = value; }
        public override List<CsvColumn<CsvColCell<string>>> Cols { get => cols; set => this.cols = value; }
    }
}
