public class ControllerFujitsu
{
    private readonly ServFujitsu service;
    private bool isInitialized = false;

    public ControllerFujitsu(ServFujitsu service)
    {
        this.service = service;
    }

    public async Task<bool> IniciarDispensador()
    {
        var status = await service.EnviarComandoInicializacion();
        
        if (status.Success)
        {
            isInitialized = true;
            return true;
        }
        return false;
    }

    public async Task<DispenseResult> DispensarEfectivo(int monto)
    {
        if (!isInitialized) throw new Exception("Dispensador no inicializado");

        var health = await service.ConsultarEstadoSensores();
        if (health.TieneErroresCriticos) 
            return DispenseResult.Error(health.ErrorCode);
        var resultado = await service.EjecutarDispensado(monto);
        await ActualizarContadoresInternos();

        return resultado;
    }

    private async Task ActualizarContadoresInternos()
    {
        var logData = await service.LeerLogMemoria();
        Console.WriteLine($"Operaciones totales: {logData.TotalPicks}");
    }
}