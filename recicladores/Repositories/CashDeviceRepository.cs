using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CashDeviceIntegration.Models;

namespace CashDeviceIntegration.Repositories
{
    public class CashDeviceRepository
    {
        private readonly HttpClient _httpClient;
        private string _token;
        private readonly string _baseUrl = "http://localhost:5000/api";

        public CashDeviceRepository()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> AuthenticateAsync(string username, string password)
        {
            var req = new AuthRequest { Username = username, Password = password };
            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/Users/Authenticate", content);
            response.EnsureSuccessStatusCode();

            var respJson = await response.Content.ReadAsStringAsync();
            var authResp = JsonSerializer.Deserialize<AuthResponse>(respJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _token = authResp.Token;

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

            return _token;
        }

        public async Task<string> OpenConnectionAsync(string comPort, int sspAddress)
        {
            var req = new OpenConnectionRequest { ComPort = comPort, SspAddress = sspAddress, EnableAcceptor = false };
            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/CashDevice/OpenConnection", content);
            response.EnsureSuccessStatusCode();

            var respJson = await response.Content.ReadAsStringAsync();
            var openResp = JsonSerializer.Deserialize<OpenConnectionResponse>(respJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return openResp.DeviceID;
        }

        public async Task EnableAcceptorAsync(string deviceId)
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/CashDevice/EnableAcceptor?deviceID={deviceId}", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task DisableAcceptorAsync(string deviceId)
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/CashDevice/DisableAcceptor?deviceID={deviceId}", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task DispenseValueAsync(string deviceId, int rawAmount, string countryCode = "MXN")
        {
            var req = new DispenseRequest { Value = rawAmount, CountryCode = countryCode };
            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/CashDevice/DispenseValue?deviceID={deviceId}", content);
            response.EnsureSuccessStatusCode();
        }
    }
}
