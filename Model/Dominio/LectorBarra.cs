namespace CajerosCfe.Model.Dominio
{
    public class LectorBarra
    {
        public string Puerto { get; set; }
        public int Velocidad { get; set; }
        public string Estado { get; set; }
        public bool Conectado { get; set; }
        public string UltimoCodigoEscaneado { get; set; }

        public LectorBarra()
        {
            Puerto = "COM6";
            Velocidad = 9600;
            Estado = "Desconocido";
        }
    }
}
