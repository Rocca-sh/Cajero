using System;
using System.Threading.Tasks;
using CashDeviceTerminal;

namespace CajeroApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await TerminalView.IniciarTerminalAsync();
        }
    }
}
