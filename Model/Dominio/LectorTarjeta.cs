namespace CajerosCfe.Model.Dominio
{
    public class LectorTarjeta
    {
        public string Puerto { get; set; }
        public int Velocidad { get; set; }
        public string Estado { get; set; }
        public bool Conectado { get; set; }
        public string UltimaLectura { get; set; }

        public LectorTarjeta()
        {
            Puerto = "COM5";
            Velocidad = 9600;
            Estado = "Desconocido";
        }
    }
}
