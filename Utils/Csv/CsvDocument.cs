using System.Collections.Generic;
using System.IO;
using System.Threading;

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

        public override void ToFile(string path)
        {
            var messageCount = cols[0].Cells.Count;
            var fStream = new FileStream(path, FileMode.OpenOrCreate);
            var sWriter = new StreamWriter(fStream);
            
            for (int i = 0; i < messageCount; i++)
            {
                var messageLine = "";
                cols.ForEach(col =>
                {
                    messageLine = string.IsNullOrEmpty(messageLine) ? col.Cells[i].Value : string.Concat(messageLine, ";", col.Cells[i].Value);
                });
                sWriter.WriteLine(messageLine);
                
            }
            sWriter.Flush();
            sWriter.Close();
        }
    }
}
