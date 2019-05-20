namespace TslWebApp.Utils.Parser
{
    public sealed class ParserFactory
    {

        public static CsvParser BuildCsvParser()
        {
            return  CsvParser.GetInstance();
        }
    }
}
