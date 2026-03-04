namespace CajerosCfe.Model.Dominio
{
    public class Aceptador
    {
        public string Puerto { get; set; }
        public int Velocidad { get; set; }
        public string Estado { get; set; }
        public bool Conectado { get; set; }
        public decimal MontoRecaudado { get; set; }

        public Aceptador()
        {
            Puerto = "COM3";
            Velocidad = 9600;
            Estado = "Desconocido";
        }
    }
}
