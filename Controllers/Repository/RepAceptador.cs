using MPOST;
using System;
using System.Threading.Tasks;


public class RepAceptador
{
    private MPOST.Acceptor mpost;
    private double Total;
    private int Meta;

    public RepAceptador()
    {
        mpost = new MPOST.Acceptor();

        mpost.OnEscrow += new EscrowEventHandler(AlDetectar);
        mpost.OnStacked += new StackedEventHandler(AlGuardar);
    }
    public async Task Conectar(string puerto)
    {
        await Task.Run(() =>
        {
            try
            {
                mpost.Open(puerto, PowerUp.A);
                mpost.EnableAcceptance = true;
                Console.WriteLine("Billetero conectado y en espera (Bloqueado).");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error de hardware: " + ex.Message);
            }
        });
    }
    public void Aceptar(int meta)
    {
        this.Meta = meta;
        this.Total = 0; // Reiniciamos el contador para la nueva meta
        mpost.EnableAcceptance = false;
        Console.WriteLine("Aceptando dinero... Meta actual: $" + meta);
    }

    private void AlDetectar(object sender, EventArgs e)
    {
        if (Total < Meta)
        {
            mpost.EscrowStack();
        }
        else
        {
            mpost.EscrowReturn(); // Devolver si ya terminamos
        }
    }
    private void AlGuardar(object sender, EventArgs e)
    {
        MPOST.Bill billete = mpost.Bill;
        double valorBillete = billete.Value;

        Total += valorBillete;

        Console.WriteLine("Ingresado: $" + valorBillete + " | Total: $" + Total);
        // Si llegamos a la meta, bloqueamos inmediatamente
        if (Total >= Meta)
        {
            mpost.EnableAcceptance = true;
            Console.WriteLine("contador alcanzado, Entrada bloqueada.");
        }
    }

};
