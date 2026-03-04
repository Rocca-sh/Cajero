using MPOST;

public class RepAceptador
{
    private readonly BillAcceptor device;

    public RepAceptador()
    {
        device = new BillAcceptor();
        device.OnNoteStacked += (s, e) => AlRecibirBillete?.Invoke(e.Value);
        device.OnStatusChanged += ManejarCambioEstado;
    }

    public void ConfigurarPuerto(string portName)
    {
        device.Open(portName, 9600); // Configuración según manual EBDS
    }

    public void Habilitar(bool status)
    {
        if (status) device.EnableAcceptance();
        else device.DisableAcceptance();
    }
}