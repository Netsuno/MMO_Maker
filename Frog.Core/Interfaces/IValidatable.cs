namespace Frog.Core.Interfaces
{
    public interface IValidatable
    {
        /// <summary>Throw InvalidDataException if invalid.</summary>
        void Validate();
    }
}
