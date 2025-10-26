namespace Frog.Core.IO
{
    public interface ISerializer<T>
    {
        T Read(Stream stream);
        void Write(Stream stream, T value);
        string Version { get; }
    }
}
