namespace CajerosCfe.Model.Dominio
{
    public class Tonelero
    {
        public string Puerto { get; set; }
        public int Velocidad { get; set; }
        public string Estado { get; set; }
        public bool Conectado { get; set; }
        public int CantidadMonedas { get; set; }

        public Tonelero()
        {
            Puerto = "COM4";
            Velocidad = 9600;
            Estado = "Desconocido";
        }
    }
}
