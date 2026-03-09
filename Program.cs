using System;
using System.Reflection;
using Custom.CuCustomWndAPI;

public class Program
{
    public static void Main(string[] args)
    {
        try
        {
            Type type = typeof(CuCustomWndDevice);
            Console.WriteLine($"Constructors for {type.Name}:");
            var ctors = type.GetConstructors();
            if (ctors.Length == 0) Console.WriteLine("No public constructors found.");
            foreach (var ctor in ctors)
            {
                var parameters = ctor.GetParameters();
                Console.Write(" - (");
                for (int i = 0; i < parameters.Length; i++)
                {
                    Console.Write($"{parameters[i].ParameterType.Name} {parameters[i].Name}");
                    if (i < parameters.Length - 1) Console.Write(", ");
                }
                Console.WriteLine(")");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inspecting CuCustomWndDevice: {ex.Message}");
        }
    }
}