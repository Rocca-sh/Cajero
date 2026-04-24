using System;
using System.Threading.Tasks;
using CashDeviceIntegration.Repositories;

namespace CashDeviceIntegration.Services
{
    public class PaymentService
    {
        private readonly CashDeviceRepository _repository;
        private string _currentDeviceId;

        // Estos Eventos son CLAVE: 
        // Tu interfaz (Vista) se suscribirá a estos eventos, sin que PaymentService sepa absolutamente nada de la Vista.
        public event Action<string> OnMessageDelivered;
        public event Action<int> OnChangeDispensed;
        public event Action OnPaymentCompleted;

        public PaymentService(CashDeviceRepository sharedRepository)
        {
            // Ahora compartes la misma conexión para billetes y monedas
            _repository = sharedRepository;
        }

        public async Task InitializeHardwareAsync(string port, int sspAddress = 6)
        {
            try
            {
                OnMessageDelivered?.Invoke("Autenticando con el servidor local...");
                await _repository.AuthenticateAsync("admin", "password");

                OnMessageDelivered?.Invoke("Buscando e intentando conectar hardware...");
                // Nota: el SspAddress suele ser 0 para billeteros, 6 para monederos.
                _currentDeviceId = await _repository.OpenConnectionAsync(port, sspAddress);
                
                OnMessageDelivered?.Invoke($"Equipo conectado exitosamente: {_currentDeviceId}");
            }
            catch (Exception ex)
            {
                OnMessageDelivered?.Invoke("Error de inicialización: " + ex.Message);
            }
        }

        public async Task StartReceivingMoneyAsync()
        {
            if (string.IsNullOrEmpty(_currentDeviceId))
            {
                OnMessageDelivered?.Invoke("Error: El equipo aún no ha sido reportado como inicializado.");
                return;
            }

            try
            {
                OnMessageDelivered?.Invoke($"Iniciando recepción - Por favor, inserte su dinero...");
                await _repository.EnableAcceptorAsync(_currentDeviceId);
                
                // NOTA FUTURA: Aquí podrías iniciar un temporizador o bucle para checar GetDeviceStatus recurrentemente
                // mientras el usuario sigue metiendo monedas.
            }
            catch (Exception ex)
            {
                OnMessageDelivered?.Invoke("Error al encender el aceptador: " + ex.Message);
            }
        }

        public async Task StopAndDispenseChangeAsync(int expectedChange)
        {
            try
            {
                OnMessageDelivered?.Invoke("Cerrando ranura de aceptación...");
                await _repository.DisableAcceptorAsync(_currentDeviceId);

                if (expectedChange > 0)
                {
                    OnMessageDelivered?.Invoke($"Entregando cambio de {expectedChange}...");
                    // Nota técnica: las APIs de ITL suelen requerir el monto x100 (sin decimales). 10 pesos = 1000
                    int rawAmount = expectedChange * 100; 
                    await _repository.DispenseValueAsync(_currentDeviceId, rawAmount);
                }

                OnMessageDelivered?.Invoke("Cobro finalizado con éxito.");
                OnChangeDispensed?.Invoke(expectedChange);
                OnPaymentCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                OnMessageDelivered?.Invoke("Error al dispensar las monedas/billetes: " + ex.Message);
            }
        }
    }
}
