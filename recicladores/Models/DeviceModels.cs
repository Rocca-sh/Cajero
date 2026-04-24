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

    public class OpenConnectionRequest
    {
        public string ComPort { get; set; }
        public int SspAddress { get; set; }
        public bool EnableAcceptor { get; set; }
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
}
