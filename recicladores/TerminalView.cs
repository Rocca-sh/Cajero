using System;
using System.Threading.Tasks;
using CashDeviceIntegration.Repositories;

namespace CashDeviceTerminal
{
    public class TerminalView
    {
        public static async Task IniciarTerminalAsync()
        {
            Console.WriteLine("=========================================");
            Console.WriteLine("    TERMINAL DE CONTROL DE RECICLADOR    ");
            Console.WriteLine("=========================================\n");

            var repository = new CashDeviceRepository();

            Console.WriteLine("Iniciando sesión en la API...");
            try 
            {
                await repository.AuthenticateAsync("admin", "admin123");
                Console.WriteLine("Sesión iniciada con éxito.\n");
            }
            catch(Exception e)
            {
                Console.WriteLine($"Error de autenticación: {e.Message}");
                // Continuamos de todas formas en caso de que estemos en un entorno sin auth por ahora.
            }

            Console.Write("Por favor, ingresa el número del puerto COM (ejemplo: 3): ");
            string puertoNumero = Console.ReadLine();
            string comPort = $"COM{puertoNumero}";

            Console.WriteLine($"\nIntentando conectar con el dispositivo en el puerto {comPort}...");
            
            string deviceId;
            try
            {
                deviceId = await repository.OpenConnectionAsync(comPort, 0);
                Console.WriteLine($"Conexión exitosa. DeviceID asignado: {deviceId}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar con el dispositivo: {ex.Message}");
                return;
            }

            bool salir = false;

            while (!salir)
            {
                Console.WriteLine("\n-----------------------------------------");
                Console.WriteLine("              MENÚ PRINCIPAL             ");
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine("1. Habilitar Aceptador (Recibir dinero)");
                Console.WriteLine("2. Deshabilitar Aceptador (Dejar de recibir dinero)");
                Console.WriteLine("3. Dispensar Dinero (Por Valor Total)");
                Console.WriteLine("4. Dispensar Dinero (Por Denominación Exacta)");
                Console.WriteLine("5. Salir");
                Console.Write("Selecciona una opción: ");
                
                string opcion = Console.ReadLine();
                Console.WriteLine();

                try
                {
                    switch (opcion)
                    {
                        case "1":
                            await repository.EnableAcceptorAsync(deviceId);
                            Console.WriteLine(">> Aceptador HABILITADO correctamente.");
                            break;
                        
                        case "2":
                            await repository.DisableAcceptorAsync(deviceId);
                            Console.WriteLine(">> Aceptador DESHABILITADO correctamente.");
                            break;
                        
                        case "3":
                            Console.Write("Ingresa la cantidad a dispensar (ej. 500): ");
                            if (int.TryParse(Console.ReadLine(), out int cantidad))
                            {
                                await repository.DispenseValueAsync(deviceId, cantidad);
                                Console.WriteLine($">> Instrucción enviada para dispensar: {cantidad} MXN.");
                            }
                            else
                            {
                                Console.WriteLine(">> Cantidad no válida.");
                            }
                            break;
                        
                        case "4":
                            Console.Write("Ingresa la denominación del billete/moneda (ej. 100): ");
                            if (!int.TryParse(Console.ReadLine(), out int denominacion)) 
                            {
                                Console.WriteLine(">> Denominación no válida.");
                                break;
                            }
                            
                            Console.Write("Ingresa la cantidad de billetes/monedas (ej. 3): ");
                            if (!int.TryParse(Console.ReadLine(), out int cantidadBilletes))
                            {
                                Console.WriteLine(">> Cantidad no válida.");
                                break;
                            }

                            await repository.DispenseByDenominationAsync(deviceId, denominacion, cantidadBilletes);
                            Console.WriteLine($">> Instrucción enviada para dispensar {cantidadBilletes} unidades de {denominacion} MXN.");
                            break;
                        
                        case "5":
                            salir = true;
                            Console.WriteLine(">> Saliendo de la terminal...");
                            break;
                        
                        default:
                            Console.WriteLine(">> Opción no válida. Intenta de nuevo.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($">> Ocurrió un error al ejecutar la orden: {ex.Message}");
                }
            }
        }
    }
}
