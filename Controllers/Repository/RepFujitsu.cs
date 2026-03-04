public class FujitsuF53Repository : IFujitsuRepository
{
    private SerialPort _serialPort;

    public async Task<bool> EnviarComandoPicking(int casete, int cantidad)
    {
        byte[] trama = { 0x10, 0x02, 0x00, 0x33, 0x60, (byte)casete, (byte)cantidad, 0x10, 0x03 };
        byte lrc = CalcularLRC(trama);
        
        _serialPort.Write(trama, 0, trama.Length);
        _serialPort.Write(new byte[] { lrc }, 0, 1);

        return await EsperarRespuestaAck();
    }

    private byte CalcularLRC(byte[] data) { /* Lógica de Checksum del manual */ }
}