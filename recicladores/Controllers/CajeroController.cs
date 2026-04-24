using System;
using System.Threading.Tasks;
using CashDeviceIntegration.Repositories;
using CashDeviceIntegration.Services;

namespace CashDeviceIntegration.Controllers
{


    public class CajeroController
    {

        private readonly CashDeviceRepository _sharedRepository;


        public PaymentService Monedero { get; private set; }
        public PaymentService Billetero { get; private set; }

        public CajeroController()
        {
            _sharedRepository = new CashDeviceRepository();

            Monedero = new PaymentService(_sharedRepository);
            Billetero = new PaymentService(_sharedRepository);
        }


        public async Task IniciarMaquinaAsync(string puertoMoneda, string puertoBillete)
        {
            await Monedero.InitializeHardwareAsync(puertoMoneda, 6);

            await Billetero.InitializeHardwareAsync(puertoBillete, 0);
        }


        public async Task ActivarCobroGeneralAsync()
        {
            await Monedero.StartReceivingMoneyAsync();
            await Billetero.StartReceivingMoneyAsync();
        }


        public async Task EntregarCambioYDeternerAsync(int cambioEnMonedas, int cambioEnBilletes)
        {
            await Monedero.StopAndDispenseChangeAsync(cambioEnMonedas);
            await Billetero.StopAndDispenseChangeAsync(cambioEnBilletes);
        }
    }
}
