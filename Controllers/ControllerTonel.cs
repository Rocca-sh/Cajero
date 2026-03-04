public class ControllerTonel
{
    private readonly ServTonel service;

    public ControllerTonel(ServTonel service)
    {
        this.service = service;
    }

    public async Task<bool> DispensarCambio(int cantidadMonedas)
    {
        try 
        {
            int entregadas = await service.DispensarYContar(cantidadMonedas);
            
            if (entregadas < cantidadMonedas)
                throw new Exception("Hopper vacío o atascado");

            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Error en dispensador de monedas", ex);
            return false;
        }
    }
}