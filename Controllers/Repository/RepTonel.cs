using System.IO.Ports;
using System.Threading.Tasks;

public class RepTonel
{
    private SerialPort comPort;

    public async Task<int> Dispensar(int totalDeseado)
    {
        int contadorPulsos = 0;

        comPort.DtrEnable = true;

        while (contadorPulsos < totalDeseado)
        {
            if (DetectarPulsoEnPin())
                contadorPulsos++;
        }

        comPort.DtrEnable = false;
        return contadorPulsos;
    }

    private bool DetectarPulsoEnPin() { return false; }
}