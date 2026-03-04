public class RepTonel
{
    private SerialPort comPort;

    public async Task<int> Dispensar(int totalDeseado)
    {
        int contadorPulsos = 0;
        
        comPort.DtrEnable = true; 

        while(contadorPulsos < totalDeseado)
        {
            if (DetectarPulsoEnPin()) 
                contadorPulsos++;
        }

        comPort.DtrEnable = false;
        return contadorPulsos;
    }
}