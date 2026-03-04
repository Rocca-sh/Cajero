public class ControllerImpresora
{
    private readonly ServImpresora service;

    public ControllerImpresora(ServImpresora service)
    {
        this.service = service;
    }

    public async Task ImprimirComprobante(TransactionData data)
    {

        var status = await service.ObtenerEstadoActual();
        
        if (status.TienePapel && !status.EstaAtascada)
        {
            await service.ImprimirTexto(data.ToFormattedString());
            await service.CortarPapel(); 
        }
        else
        {
            throw new InvalidOperationException("Impresora no disponible: " + status.DetalleError);
        }
    }
}