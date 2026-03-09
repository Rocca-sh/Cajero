using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Threading.Tasks;

public class RepImpresora
{
    private PrintDocument pcImpresora;
    private string TextoParaTicket;

    public RepImpresora()
    {
        pcImpresora = new PrintDocument();
        pcImpresora.PrintPage += new PrintPageEventHandler(AlImprimir);
    }

    public async Task Conectar(string nombreImpresoraWindows)
    {
        await Task.Run(() =>
        {
            try
            {
                pcImpresora.PrinterSettings.PrinterName = nombreImpresoraWindows;

                if (pcImpresora.PrinterSettings.IsValid)
                {
                    Console.WriteLine("Impresora S.O. conectada y validada.");
                }
                else
                {
                    Console.WriteLine(" La impresora no fue encontrada en Windows.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error de sistema operativo: " + ex.Message);
            }
        });
    }

    public void ImprimirTicket(string texto)
    {
        this.TextoParaTicket = texto;

        try
        {
            Console.WriteLine("Mandando a imprimir...");
            pcImpresora.Print(); 
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error al intentar imprimir: " + ex.Message);
        }
    }

    private void AlImprimir(object sender, PrintPageEventArgs e)
    {
        Font fuenteTicket = new Font("Courier New", 10);
        SolidBrush tinta = new SolidBrush(Color.Black);

        e.Graphics.DrawString(TextoParaTicket, fuenteTicket, tinta, 10, 10);

        Console.WriteLine("Impresión finalizada por el Spooler de Windows.");

        e.HasMorePages = false;
    }
}
