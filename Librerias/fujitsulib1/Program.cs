using System;
using System.Threading;
using System.Threading.Tasks;
using FujitsuBDU;

/// <summary>
/// Example usage of the FujitsuBDU SDK.
/// Demonstrates: connection, status check, initialize, dispense, error handling.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        string port = args.Length > 0 ? args[0] : "COM3";
        Console.WriteLine($"Fujitsu F53/F56 BDU SDK — connecting on {port}");
        Console.WriteLine(new string('─', 50));

        // ── 1. Error code lookup demo (no hardware needed) ───────
        DemoErrorCodes();

        // ── 2. Hardware communication ────────────────────────────
        Console.WriteLine("\nPress any key to connect to device (or Ctrl+C to exit)...");
        Console.ReadKey(true);

        using var bdu = new BduDriver(port);

        try
        {
            bdu.Open();
            Console.WriteLine("✓ Serial port opened.");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // ── Get status ───────────────────────────────────────
            Console.WriteLine("\n[1] Reading device status...");
            var status = await bdu.GetStatusAsync(cts.Token);
            PrintStatus(status);

            if (status.Status == DeviceStatus.Error)
            {
                Console.WriteLine("Device in error state. Attempting reset...");
                var resetResp = await bdu.ResetAsync(cts.Token);
                Console.WriteLine(resetResp.Success ? "✓ Reset OK" : $"✗ Reset failed: {resetResp.ErrorMessage}");
            }

            // ── Initialize ───────────────────────────────────────
            Console.WriteLine("\n[2] Initializing device...");

            // Example: 4 cassettes, all with same length/thickness
            // Length 0xAF8C ≈ 45068 (Fujitsu uses fixed-point tenths of mm)
            ushort[] lengths    = { 0xAF8C, 0xAF8C, 0xAF8C, 0xAF8C };
            byte[]   thickness  = { 0x0C,   0x0C,   0x0C,   0x0C   };

            var initResp = await bdu.InitializeAsync(lengths, thickness, cts.Token);
            if (initResp.Success)
                Console.WriteLine("✓ Initialize OK");
            else
                Console.WriteLine($"✗ Initialize failed: {initResp.ErrorMessage}");

            // ── Dispense ─────────────────────────────────────────
            Console.WriteLine("\n[3] Dispensing 500 (cassette 1: 5 × $100)...");

            var dispenseResult = await bdu.DispenseAsync(
                totalAmount: 500,
                cassetteCounts: new[]
                {
                    ((ushort)100, (ushort)5),  // cassette 1: 5 bills of $100
                    ((ushort)0,   (ushort)0),  // cassette 2: not used
                    ((ushort)0,   (ushort)0),  // cassette 3: not used
                    ((ushort)0,   (ushort)0),  // cassette 4: not used
                },
                ct: cts.Token
            );

            if (dispenseResult.Success)
            {
                Console.WriteLine($"✓ Dispensed {dispenseResult.NotesDispensed} notes.");

                // Wait and then present
                await Task.Delay(1000, cts.Token);
                var presentResp = await bdu.PresentAsync(cts.Token);
                Console.WriteLine(presentResp.Success ? "✓ Notes presented at slot." : $"✗ Present failed: {presentResp.ErrorMessage}");
            }
            else
            {
                Console.WriteLine($"✗ Dispense failed: {dispenseResult.ErrorMessage}");
                if (!string.IsNullOrEmpty(dispenseResult.ErrorCode))
                    Console.WriteLine($"  Error code: {dispenseResult.ErrorCode}");
            }
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"✗ Cannot open {port} — check if port exists and is not in use.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✗ Operation timed out.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Unexpected error: {ex.Message}");
        }

        Console.WriteLine("\nDone. Press any key to exit.");
        Console.ReadKey(true);
    }

    // ── Helpers ──────────────────────────────────────────────────

    static void PrintStatus(StatusResponse s)
    {
        Console.WriteLine($"  Status     : {s.Status}");
        Console.WriteLine($"  Description: {s.Description}");
        Console.WriteLine($"  Cassettes  : {(s.Cassette1Present?"[1]":"[ ]")} " +
                                          $"{(s.Cassette2Present?"[2]":"[ ]")} " +
                                          $"{(s.Cassette3Present?"[3]":"[ ]")} " +
                                          $"{(s.Cassette4Present?"[4]":"[ ]")}");
        Console.WriteLine($"  Media@Exit : {s.MediaAtEjection}");
        Console.WriteLine($"  Media@Pool : {s.MediaAtPool}");
        Console.WriteLine($"  ShutterOpen: {s.ShutterOpen}");
        Console.WriteLine($"  RejectFull : {s.RejectBoxFull}");
    }

    static void DemoErrorCodes()
    {
        Console.WriteLine("Error code interpreter demo:");
        Console.WriteLine(new string('─', 50));

        // FW error codes (semimajor, additional)
        var fwCodes = new (byte, byte, string)[]
        {
            (0x78, 0x01, "JAM at FDLS1"),
            (0x18, 0x00, "1st Cassette pick error"),
            (0x20, 0x00, "No 2nd Cassette"),
            (0x50, 0x02, "No bill in pool section"),
            (0xA1, 0x00, "Front shutter open error"),
            (0xB5, 0x00, "Reject box full"),
            (0x84, 0x00, "Thickness error"),
            (0xF8, 0x01, "Sensor error FDLS1"),
        };

        Console.WriteLine("FW Error Codes:");
        foreach (var (s, a, note) in fwCodes)
        {
            string interpreted = BduErrorInterpreter.Interpret(s, a);
            Console.WriteLine($"  0x{s:X2} 0x{a:X2}  →  {interpreted}");
        }

        // SP error codes
        Console.WriteLine("\nSP Error Codes:");
        var spCodes = new[] { "$$0101", "$$5801", "$$0C01", "$$7807", "$$3201" };
        foreach (var code in spCodes)
        {
            string interpreted = BduErrorInterpreter.InterpretSpError(code);
            Console.WriteLine($"  {code,-12}  →  {interpreted}");
        }

        Console.WriteLine();
    }
}
