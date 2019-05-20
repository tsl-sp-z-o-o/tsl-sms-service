namespace TslWebApp.Utils
{
    public abstract class AbstractCell<T>
    {
        public abstract int Index { get; set; }
        public abstract T Value { get; set; }
    }
}
