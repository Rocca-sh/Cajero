using CajerosCfe.Model.Servicio.Aceptador;

public class ControllerAceptador
{
    private readonly ServAceptador servicio;
    public decimal TotalIngresado { get; private set; }

    public  ControllerAceptador(ServAceptador servicio)
    {
       this.servicio = servicio;
    }

    public async Task IniciarCobro(decimal montoObjetivo)
    {
        TotalIngresado = 0;
        await servicio.HabilitarAceptacion(true);
        
        servicio.AlRecibirBillete += (monto) => {
            TotalIngresado += monto;
            Console.WriteLine($"Billete de {monto} aceptado. Total: {TotalIngresado}");
        };
    }

    public async Task DetenerCobro()
    {
        await servicio.HabilitarAceptacion(false);
    }
}
