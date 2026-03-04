namespace CajerosCfe.Model.Dominio
{
    public class Impresora
    {
        public string Puerto { get; set; }
        public int Velocidad { get; set; }
        public string Estado { get; set; }
        public bool TienePapel { get; set; }
        public bool EnLinea { get; set; }

        public Impresora()
        {
            Puerto = "COM1";
            Velocidad = 9600;
            Estado = "Desconocido";
        }
    }
}
