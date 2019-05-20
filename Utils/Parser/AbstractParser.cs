using System.Threading.Tasks;

namespace TslWebApp.Utils.Parser
{
    public abstract class AbstractParser<T>
    {
        public abstract ParserType ParserType { get; }
        public abstract Task<T> Parse(string absoluteFilePath, bool containsHeaders);
    }
}
