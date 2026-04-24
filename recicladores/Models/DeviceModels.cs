namespace CashDeviceIntegration.Models
{
    public class AuthRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class AuthResponse
    {
        public string Token { get; set; }
    }

    public class InhibitConfig
    {
        public string Denomination { get; set; }
        public bool Inhibit { get; set; }
    }

    public class OpenConnectionRequest
    {
        public string ComPort { get; set; }
        public int SspAddress { get; set; }
        public bool EnableAcceptor { get; set; }
        public string LogFilePath { get; set; }
        public InhibitConfig[] SetInhibits { get; set; }
    }

    public class OpenConnectionResponse
    {
        public string DeviceID { get; set; }
        public bool IsOpen { get; set; }
    }

    public class DispenseRequest
    {
        public int Value { get; set; }
        public string CountryCode { get; set; }
    }
    public class DispenseByDenominationRequest
    {
        public int Denomination { get; set; }
        public int Count { get; set; }
        public string CountryCode { get; set; }
    }
}
