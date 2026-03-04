using Custom_VKP_XFS;

public class PrinterRepository  
{
    private SerialPort printerPort;

    public void ImprimirTexto(string texto)
    {
        printerPort.WriteLine(texto);
    }

    public void EjecutarCorteDePapel()
    {
        byte[] cutCommand = { 0x1B, 0x69 }; 
        printerPort.Write(cutCommand, 0, cutCommand.Length);
    }

    public string ObtenerEstado()
    {
        _printerPort.Write(new byte[] { 0x10, 0x04, 0x01 }, 0, 3);
        return TraducirRespuestaEstado(_printerPort.ReadByte());
    }
}