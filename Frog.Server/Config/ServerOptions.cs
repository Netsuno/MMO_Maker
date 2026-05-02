namespace Frog.Server.Config
{
    public sealed class ServerOptions
    {
        public string BindAddress { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 6000;

        public void Validate()
        {
            // Préférence de l’analyzer : motif (IDE0078)
            if (Port is <= 0 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(Port), "Le port doit être entre 1 et 65535.");
            }

            // Validation IP optionnelle
            // if (!System.Net.IPAddress.TryParse(BindAddress, out _))
            // {
            //     throw new ArgumentException("BindAddress invalide.");
            // }
        }
    }
}
