namespace TslWebApp.Utils.Csv
{
    public class CsvColCell<T> : AbstractCell<T>
    {
        private T cellValue;
        public override int Index { get; set; }

        public override T Value { get => cellValue; set => cellValue = value; }

        public CsvColumn<CsvColCell<T>> ParentColumn { get; set; }
    }
}
