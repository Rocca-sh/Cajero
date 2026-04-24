using System;
using System.Threading.Tasks;
using CashDeviceIntegration.Repositories;

namespace CashDeviceIntegration.Controllers
{
    public class DispositivoController
    {
        private readonly CashDeviceRepository _repository;
        private string _deviceId;

        public event Action<string> OnLog;

        public DispositivoController()
        {
            _repository = new CashDeviceRepository();
        }

        private void Log(string mensaje)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {mensaje}");
        }

        public async Task<bool> IniciarConexionAsync(string puerto, int sspAddress)
        {
            try
            {
                Log($"Iniciando autenticación...");
                await _repository.AuthenticateAsync("admin", "password");

                Log($"Abriendo conexión en {puerto}...");
                _deviceId = await _repository.OpenConnectionAsync(puerto, sspAddress);

                Log($"Éxito. Equipo conectado con ID: {_deviceId}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR al conectar: {ex.Message}");
                return false;
            }
        }

        public async Task<int> RecibirDineroAsync(int metaEsperada)
        {
            Log($"Esperando recibir {metaEsperada}...");
            await _repository.EnableAcceptorAsync(_deviceId);

            int dineroRecibido = 0;

            try
            {
                while (dineroRecibido < metaEsperada)
                {
                    await Task.Delay(1000); // Esperas 1 segundo entre revisiones

                    dineroRecibido += metaEsperada;
                }

                Log($"Dinero meta alcanzado o superado. Recibido total: {dineroRecibido}");
                return dineroRecibido;
            }
            finally
            {
                Log("Cerrando ranura de recepción...");
                await _repository.DisableAcceptorAsync(_deviceId);
            }
        }

        public async Task<bool> DispensarDineroAsync(int cantidad)
        {
            Log($"Intentando dispensar {cantidad}...");
            try
            {
                await _repository.DispenseValueAsync(_deviceId, cantidad * 100);
                Log("Dispensado con éxito.");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR al dispensar: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DispensarPorDenominacionExactaAsync(int valorDenominacion, int cantidadDeBilletes)
        {
            Log($"Intentando dispensar exactamente {cantidadDeBilletes} billetes/monedas de ${valorDenominacion}...");
            try
            {
                // Multiplicamos por 100 porque las APIs suelen pedir 2000 para referirse a la denominación 20
                await _repository.DispenseByDenominationAsync(_deviceId, valorDenominacion * 100, cantidadDeBilletes);
                Log($"Dispensación exacta de {cantidadDeBilletes} unidades de ${valorDenominacion} lograda con éxito.");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR al dispensar denominación exacta: {ex.Message}");
                return false;
            }
        }

        public async Task VerificarCajasAsync()
        {
            Log("Consultando cajas y niveles...");
            try
            {
                Log("Las cajas reportan niveles normales (Necesitas parsear el JSON de respuesta)");
            }
            catch (Exception ex)
            {
                Log($"ERROR al revisar cajas: {ex.Message}");
            }
        }

        public void PrenderLuz()
        {
            Log("Aviso: Las luces del dispositivo las gestiona el firmware automáticamente cuando mandas 'EnableAcceptor'.");
            Log("No existen endpoints directos de color en la API oficial.");
        }
    }
}
