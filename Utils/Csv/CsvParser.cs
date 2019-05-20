using System.IO;
using System.Threading.Tasks;
using TslWebApp.Utils.Csv;
using TslWebApp.Utils.Parser;
using System.Linq;
using System.Collections.Generic;

namespace TslWebApp.Utils
{
    public sealed class CsvParser : AbstractParser<CsvDocument>
    {
        private volatile static CsvParser _parserInstance = new CsvParser();


        public override ParserType ParserType { get => ParserType.CSV; }

        private CsvParser() { }

        public static CsvParser GetInstance()
        {
            return _parserInstance;
        }

        public override async Task<CsvDocument> Parse(string absoluteFilePath, bool containsHeader)
        {
            var rawLines = await File.ReadAllLinesAsync(absoluteFilePath);
            var csvDocument = new CsvDocument();
            csvDocument.Title = Path.GetFileNameWithoutExtension(absoluteFilePath);
            var cols = new List<CsvColumn<CsvColCell<string>>>();
            var colCount = rawLines[0].Split(";").Length;

            for (int i = 0; i < colCount; i++)
            {
                var column = new CsvColumn<CsvColCell<string>>("Header"+i, 
                                                               new List<CsvColCell<string>>());
                cols.Add(column);
            }
            
            rawLines.ToList().ForEach(line => 
            {
                var row = line.Split(';');
                for (int i = 0; i < row.Length; i++)
                {
                    cols[i].Cells.Add(new CsvColCell<string>()
                    {
                        Index = cols[i].Length + 1,
                        Value = row[i],
                        ParentColumn = cols[i]
                    });
                }
            });
            csvDocument.Cols = cols;
            return csvDocument;
        }
    }
}
