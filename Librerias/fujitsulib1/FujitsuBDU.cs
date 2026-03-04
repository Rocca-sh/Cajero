using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FujitsuBDU
{
    // ─────────────────────────────────────────────────────────────
    //  ENUMS & CONSTANTS
    // ─────────────────────────────────────────────────────────────

    public enum BduCommand : byte
    {
        GetStatus       = 0x60,
        Initialize      = 0x60,
        BillCount       = 0x60,
        Reset           = 0x60,
    }

    public enum BduCommandCode : byte
    {
        GetStatus   = 0x01,
        Initialize  = 0x02,
        BillCount   = 0x03,
        Reset       = 0x08,
        Present     = 0x09,
        Retract     = 0x0A,
        Reject      = 0x0B,
    }

    /// <summary>
    /// High-level device status derived from the status response.
    /// </summary>
    public enum DeviceStatus
    {
        Unknown,
        Ready,
        Busy,
        Error,
        NoCassette,
        CassetteEmpty,
        JamDetected,
        MediaRemaining,
    }

    // ─────────────────────────────────────────────────────────────
    //  RESULT / RESPONSE TYPES
    // ─────────────────────────────────────────────────────────────

    public class BduResponse
    {
        public bool     Success     { get; init; }
        public byte[]   RawData     { get; init; } = Array.Empty<byte>();
        public string   ErrorMessage{ get; init; } = string.Empty;

        /// <summary>FW error code bytes from response (command byte + error byte).</summary>
        public byte[]   FwErrorCode { get; init; } = Array.Empty<byte>();

        public static BduResponse Ok(byte[] data) =>
            new() { Success = true, RawData = data };

        public static BduResponse Fail(string msg, byte[]? errCode = null) =>
            new() { Success = false, ErrorMessage = msg,
                    FwErrorCode = errCode ?? Array.Empty<byte>() };
    }

    public class StatusResponse
    {
        public DeviceStatus Status      { get; init; }
        public byte[]       RawStatus   { get; init; } = Array.Empty<byte>();
        public string       Description { get; init; } = string.Empty;

        // Cassette info (bytes from status payload)
        public bool Cassette1Present    { get; init; }
        public bool Cassette2Present    { get; init; }
        public bool Cassette3Present    { get; init; }
        public bool Cassette4Present    { get; init; }

        public bool MediaAtEjection     { get; init; }
        public bool MediaAtPool         { get; init; }
        public bool ShutterOpen         { get; init; }
        public bool RejectBoxFull       { get; init; }
    }

    public class DispenseResult
    {
        public bool     Success         { get; init; }
        public int      NotesDispensed  { get; init; }
        public string   ErrorMessage    { get; init; } = string.Empty;
        public string   ErrorCode       { get; init; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────
    //  ERROR CODE INTERPRETER
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Interprets F56-BDU firmware error codes (2-byte: semimajor + additional).
    /// Based on F56-BDU ERROR CODE LIST K3KD03234-0001 E03.
    /// </summary>
    public static class BduErrorInterpreter
    {
        public static string Interpret(byte semimajor, byte additional)
        {
            // Upper nibble = section, lower nibble = detail
            byte section = (byte)(semimajor & 0xF0);
            byte detail  = (byte)(semimajor & 0x0F);

            return section switch
            {
                0x00 => InterpretSystemDown(detail, additional),
                0x10 => InterpretCassette(1, 5, detail, additional),
                0x20 => InterpretCassette(2, 6, detail, additional),
                0x30 => InterpretCassette(3, 7, detail, additional),
                0x40 => InterpretCassette(4, 8, detail, additional),
                0x50 => InterpretPool(detail, additional),
                0x70 => InterpretTransfer(detail, additional),
                0x78 => InterpretJam(detail, additional),
                0x7A => $"JAM: DFSS→REJS area",
                0x7B => $"JAM: REJS sensor error (add=0x{additional:X2})",
                0x7C => InterpretEjectionJam(detail, additional),
                0x7D => $"JAM: EJSF/EJSR→Pool section",
                0x80 => $"Bill length error (LONG)",
                0x82 => $"Bill length error (SHORT)",
                0x83 => $"Bill SHORT (add=0x{additional:X2})",
                0x84 => $"Thickness error (add=0x{additional:X2})",
                0x85 => $"Pick from wrong cassette #{additional}",
                0x86 => $"Spacing error between bills",
                0x88 => InterpretCountMismatch(additional),
                0x89 => $"Potentiometer error (DFCS=0x{additional:X2})",
                0xA0 => $"(Front) Shutter open error",
                0xA1 => InterpretShutterError("Front", "open", additional),
                0xA2 => InterpretShutterError("Front", "close", additional),
                0xA4 => $"(Front) No medium in ejection section (sensors=0x{additional:X2})",
                0xA7 => $"(Front) Shutter open detected (sensors=0x{additional:X2})",
                0xA9 => InterpretShutterError("Rear", "open", additional),
                0xAA => InterpretShutterError("Rear", "close", additional),
                0xAC => $"(Rear) No medium in ejection section (sensors=0x{additional:X2})",
                0xAF => $"(Rear) Shutter open detected (sensors=0x{additional:X2})",
                0xB0 => $"Reject box not set (sensors=0x{additional:X2})",
                0xB5 => $"Reject box FULL (sensors=0x{additional:X2})",
                0xB8 => $"(Front) Capture box option not set",
                0xB9 => $"(Front) Capture box not set",
                0xBA => $"(Front) Capture box FULL (sensors=0x{additional:X2})",
                0xBC => $"(Rear) Capture box option not set",
                0xBD => $"(Rear) Capture box not set",
                0xBE => $"(Rear) Capture box FULL (sensors=0x{additional:X2})",
                0xC0 => InterpretDownload(detail, additional),
                0xC1 => InterpretDownloadError(detail),
                0xE0 => $"RAS command undefined",
                0xE1 => $"No bill information registered for initialization",
                0xE4 => $"Bill info not provided for cassette #{additional}",
                0xE5 => $"Count sequence specification error",
                0xE6 => $"Parameter ISO code error (data=0x{additional:X2})",
                0xE8 => $"Bill length/thickness info error (data=0x{additional:X2})",
                0xEA => $"Parameter error (data=0x{additional:X2})",
                0xEC => $"FS error (data=0x{additional:X2})",
                0xEE => $"Command format error (data=0x{additional:X2})",
                0xEF => $"Command execution impossible – pool not set up (sensors=0x{additional:X2})",
                0xF1 => $"Over current error",
                0xF2 => $"Option setup unusual (sensors=0x{additional:X2})",
                0xF3 => $"(Front) Option setup unusual (sensors=0x{additional:X2})",
                0xF4 => $"(Rear) Option setup unusual (sensors=0x{additional:X2})",
                0xF6 => $"Log data checksum error (sum=0x{additional:X2})",
                0xF8 => InterpretSensorError(additional),
                0xFC => $"Illegal operation – F56S no data notification",
                0xFD => $"Power off during count – F56S",
                _    => $"Unknown error 0x{semimajor:X2} 0x{additional:X2}"
            };
        }

        private static string InterpretSystemDown(byte detail, byte add) => detail switch
        {
            0x01 => "System down: Internal error 1",
            0x02 => "System down: Internal error 2",
            0x04 => $"System down: Internal error 3 (Task ID=0x{add:X2})",
            _    => $"System down: Unknown (0x0{detail:X1})"
        };

        private static string InterpretCassette(int cas, int cas2, byte detail, byte add)
        {
            int slot = (detail & 0x04) != 0 ? cas2 : cas;
            return (detail & 0x0F) switch
            {
                0x00 => $"No cassette #{slot}",
                0x01 => $"Cassette #{slot} empty (low pick error)",
                0x02 => InterpretDiagError(slot, add),
                0x04 => InterpretDiagError(slot, add),
                0x06 => $"Cassette #{slot} pick error",
                0x08 => $"Cassette #{slot} pick error",
                _    => $"Cassette #{slot} unknown detail 0x{detail:X2}"
            };
        }

        private static string InterpretDiagError(int slot, byte add) => add switch
        {
            0x00 => $"Cassette #{slot}: Diagnosis data none",
            0x01 => $"Cassette #{slot}: Denomination differs",
            0x02 => $"Cassette #{slot}: Length reference value cannot be fixed",
            0x03 => $"Cassette #{slot}: Thickness reference value cannot be fixed",
            0x04 => $"Cassette #{slot}: Length diagnosis result out of range",
            0x05 => $"Cassette #{slot}: Thickness diagnosis result out of range (0.09–0.15mm)",
            _    => $"Cassette #{slot}: Diagnosis error 0x{add:X2}"
        };

        private static string InterpretPool(byte detail, byte add) => detail switch
        {
            0x00 => $"Pool home position error (add=0x{add:X2})",
            0x01 => $"Pool upper position error (add=0x{add:X2})",
            0x02 => $"No medium in pool section (sensors=0x{add:X2})",
            _    => $"Pool section error 0x5{detail:X1} (add=0x{add:X2})"
        };

        private static string InterpretTransfer(byte detail, byte add) => detail switch
        {
            0x00 => $"Medium remaining at FDLS/DFSS/BPS/BRS sensors (add=0x{add:X2})",
            0x06 => "Medium pulled out during bill retrieval (EJSF)",
            0x07 => "Medium pulled out during bill retrieval (EJSR)",
            _    => $"Transfer section error (detail=0x{detail:X2}, add=0x{add:X2})"
        };

        private static string InterpretJam(byte detail, byte add)
        {
            if (add >= 0x01 && add <= 0x08) return $"JAM at FDLS{add}";
            if (add >= 0x11 && add <= 0x18) return $"JAM between FDLS{add - 0x10} and DFSS";
            if (add >= 0x21 && add <= 0x28) return $"FDLS{add - 0x20} medium remaining after count";
            if (add >= 0x31 && add <= 0x38) return $"FDLS{add - 0x30} medium remaining after JAM clear";
            if (add >= 0x41 && add <= 0x48) return $"FDLS{add - 0x40} turned on with no bill";
            return add switch {
                0x29 => "DFSS medium remaining after count",
                0x2A => "BPS medium remaining after count",
                0x39 => "DFSS medium remaining after JAM clear",
                0x3A => "BPS medium remaining after JAM clear",
                0x49 => "DFSS turned on with no bill",
                _    => $"JAM section error (add=0x{add:X2})"
            };
        }

        private static string InterpretEjectionJam(byte detail, byte add) => add switch
        {
            0x01 => "JAM: Pool→EJSF",
            0x02 => "JAM: Pool→EJSR",
            0x03 => "JAM: EJSF+BRS2 simultaneous detection",
            0x04 => "JAM: EJSF+BRS1 simultaneous detection",
            0x05 => "JAM: EJSR+BRS3 simultaneous detection",
            0x2C => "EJSF medium remaining after count",
            0x2D => "EJSR medium remaining after count",
            0x3C => "EJSF medium remaining after JAM clear",
            0x3D => "EJSR medium remaining after JAM clear",
            0x80 => "EJSF sensor on before mecha reset / feed operation",
            0x81 => "EJSR sensor on before mecha reset / feed operation",
            _    => $"Ejection JAM (add=0x{add:X2})"
        };

        private static string InterpretShutterError(string side, string dir, byte add) => add switch
        {
            0x00 => $"({side}) Shutter {dir} error: SCS{(side=="Front"?"F":"R")} did not change",
            0x01 => $"({side}) Shutter {dir} error: SOS{(side=="Front"?"F":"R")} did not change",
            0x02 => $"({side}) Shutter {dir} error: Simultaneous sensor detection",
            _    => $"({side}) Shutter {dir} error (add=0x{add:X2})"
        };

        private static string InterpretCountMismatch(byte add) => add switch
        {
            0x00 => "Count mismatch: Requested ≠ Normal identified ≠ passed through BPS",
            0x01 => "Count mismatch: BPS turned ON with no bill",
            0x03 => "Count mismatch: Medium passed through BPS while clearing JAM",
            _    => $"Count mismatch (add=0x{add:X2})"
        };

        private static string InterpretDownload(byte detail, byte add) => add switch
        {
            0x00 => "Download: D-level command received during RAS mode",
            0x01 => "Download: LE received during RAM program execution",
            0x02 => "Download: LE received before LD command",
            _    => $"Download sequence error (add=0x{add:X2})"
        };

        private static string InterpretDownloadError(byte detail) => detail switch
        {
            0x00 => "Download: Loss in download program after RT",
            0x01 => "Download: Flash ROM write error",
            0x02 => "Download: Sum check error",
            0x03 => "Download: Version error after RT",
            0x04 => "Download: Flash ROM erase error",
            0x05 => "Download: File name error in control area format",
            0x06 => "Download: Data size error in control area format",
            _    => $"Download error (detail=0x{detail:X2})"
        };

        private static string InterpretSensorError(byte add)
        {
            if (add >= 0x01 && add <= 0x0F)
            {
                string[] sensors = { "", "FDLS1","FDLS2","FDLS3","FDLS4",
                                     "FDLS5","FDLS6","DFSS","REJS","BPS",
                                     "BRS1","BRS2","BRS3","EJSR","EJSF","BCS(op)" };
                return $"Sensor slice level error: {sensors[add]}";
            }
            if (add >= 0x81 && add <= 0x8F)
            {
                return $"Sensor-off check error (sensor 0x{add:X2})";
            }
            if (add >= 0xA1 && add <= 0xAF)
            {
                return $"Sensor-on check error (sensor 0x{add:X2})";
            }
            if (add == 0xF0) return "Sensor level write DAC error";
            return $"Sensor error (add=0x{add:X2})";
        }

        /// <summary>
        /// Interpret SP-level error codes (e.g. "$$0101") from GBDU SP Error Code List.
        /// </summary>
        public static string InterpretSpError(string spCode)
        {
            if (spCode.StartsWith("$$")) spCode = spCode[2..];
            if (!int.TryParse(spCode, System.Globalization.NumberStyles.HexNumber, null, out int code))
                return $"Unknown SP error: {spCode}";

            int category = (code >> 8) & 0xFF;
            int detail   = code & 0xFF;

            return category switch
            {
                0x01 => $"SP 01{detail:X2}: Media remaining at {SpMediaLocation(detail)}",
                0x05 => $"SP 05{detail:X2}: Shutter already open before command ({SpCommandName(detail)})",
                0x07 => $"SP 07xx: Misfeeding – check motor, sensor, connector, belt",
                0x09 => $"SP 09xx: Max unrecognized notes – check dispensing notes",
                0x0C => detail switch {
                    0x01 => "SP 0C01: No media at pool section (PrePresent)",
                    0x02 => "SP 0C02: No media at bill exit (Retract)",
                    0x03 => "SP 0C03: No media at pool/exit (Reject)",
                    0x04 => "SP 0C04: Shutter not closed (Reject)",
                    0x05 => "SP 0C05: No media at pool/exit (Present)",
                    _    => $"SP 0C{detail:X2}: No feeding notes"
                },
                0x0D => "SP 0Dxx: Reject box full – remove notes",
                0x13 => "SP 13xx: No cassettes set",
                0x15 => $"SP 15{detail:X2}: RS-232C send error – check cable",
                0x16 => "SP 16xx: Receive error – check USB cable",
                0x30 => "SP 30xx: RAS working – wait until RAS operation ends",
                0x32 => $"SP 32{detail:X2}: Dispense impossible – {SpDispenseImpossible(detail)}",
                0x37 => "SP 37xx: Test operation impossible for all cassettes",
                0x38 => "SP 38xx: System error – restart device",
                0x51 => "SP 51xx: Status error (internal) – restart device",
                0x52 => "SP 52xx: Abnormal data received from FW – restart device",
                0x54 => "SP 54xx: Defined file read error – check FW file at C:\\GBDU\\FW",
                0x55 => "SP 55xx: FW download error – re-download firmware",
                0x57 => "SP 57xx: Version number check error – restart device",
                0x58 => $"SP 58{detail:X2}: File open/write error – check file existence",
                0x59 => "SP 59xx: Circuit initialization error – restart device",
                0x5A => $"SP 5A{detail:X2}: Parameter error – check input parameters",
                0x60 => "SP 60xx: Unit is not G510 with banknote reader",
                0x64 => "SP 64xx: Money trouble – actual count differs from requested",
                0x70 => "SP 70xx: Internal logic error – restart device",
                0x71 => "SP 71xx: Internal function error – restart device",
                0x72 => $"SP 72{detail:X2}: Parameter error – check WFS command parameters",
                0x73 => $"SP 73{detail:X2}: Exchange error – cancel Exchange Active",
                0x74 => $"SP 74{detail:X2}: Device not ready – {SpDeviceNotReady(detail)}",
                0x76 => "SP 76xx: Cash unit error – check cassette denomination registry",
                0x77 => "SP 77xx: Lock error – call WFSUnLock",
                0x7B => "SP 7Bxx: Reject box full – remove notes from reject box",
                0x7C => "SP 7Cxx: Timeout error – check timer settings",
                0x7D => "SP 7Dxx: Cancel – no action needed",
                _    => $"SP {category:X2}{detail:X2}: Unknown SP error"
            };
        }

        private static string SpMediaLocation(int detail) => detail switch
        {
            0x01 => "bill exit (Dispense/Denominate)",
            0x02 or 0x03 => "pool section (MechaInitialize)",
            0x04 or 0x05 => "pool section (Reset)",
            0x06 or 0x07 or 0x08 or 0x09 => "pool section (Retract/Reject)",
            _    => $"unknown location (0x{detail:X2})"
        };

        private static string SpCommandName(int detail) => detail switch
        {
            0x01 => "TestOpenShutter",
            0x02 => "Present",
            0x03 or 0x04 => "Dispense",
            _    => $"command 0x{detail:X2}"
        };

        private static string SpDispenseImpossible(int detail) => detail switch
        {
            0x01 => "fwDispenser is WFS_CDM_DISPCUSTOP",
            0x02 => "No cash unit matches cCurrencyID",
            0x03 => "ulDenomAmount and per-cassette count not set",
            _    => "denomination calculation failed"
        };

        private static string SpDeviceNotReady(int detail) => detail switch
        {
            0x07 => "CashIn Active error (Dispense)",
            0x08 => "CashIn Active error (Open Shutter)",
            0x09 => "CashIn Active error (Present)",
            0x0A => "CashIn Active error (Close Shutter)",
            0x0B => "Media remaining (Close Shutter)",
            0x0C => "Media remaining (Open Shutter)",
            _    => $"device not ready (0x{detail:X2})"
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  CRC CALCULATOR  (X^16 + X^12 + X^5 + 1 = CRC-16/CCITT)
    // ─────────────────────────────────────────────────────────────

    internal static class Crc16
    {
        /// <summary>
        /// Calculates CRC-16/CCITT (poly 0x1021) as specified by F56-BDU RS232C spec.
        /// Covers from the byte after STX through ETX (inclusive).
        /// Returns [high, low].
        /// </summary>
        public static byte[] Calculate(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int j = 0; j < 8; j++)
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ 0x1021)
                        : (ushort)(crc << 1);
            }
            return new[] { (byte)(crc >> 8), (byte)(crc & 0xFF) };
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  FRAME BUILDER / PARSER
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds and parses DLE-framed RS232C packets per F56-BDU spec.
    /// Frame: DLE STX [LENGTH H L] [DATA...] DLE ETX [CRC H L]
    /// CRC covers: bytes from LENGTH start through ETX.
    /// </summary>
    internal static class FrameCodec
    {
        private const byte DLE = 0x10;
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const byte ENQ = 0x05;
        private const byte ACK = 0x06;
        private const byte NAK = 0x15;

        public static byte[] BuildEnq()   => new[] { DLE, ENQ };
        public static byte[] BuildAck()   => new[] { DLE, ACK };
        public static byte[] BuildNak()   => new[] { DLE, NAK };

        /// <summary>Wraps payload bytes in a full DLE-STX...DLE-ETX frame with CRC.</summary>
        public static byte[] BuildFrame(byte[] payload)
        {
            // LENGTH field = payload length as 2 bytes
            ushort len = (ushort)payload.Length;
            byte lenH = (byte)(len >> 8);
            byte lenL = (byte)(len & 0xFF);

            // Build the CRC-covered region: LENGTH + DATA + ETX
            var crcRegion = new List<byte> { lenH, lenL };
            crcRegion.AddRange(payload);
            crcRegion.Add(ETX);

            byte[] crc = Crc16.Calculate(crcRegion.ToArray(), 0, crcRegion.Count);

            // Assemble full frame (DLE stuffing: any DLE in data is doubled)
            var frame = new List<byte> { DLE, STX, lenH, lenL };
            foreach (byte b in payload)
            {
                frame.Add(b);
                if (b == DLE) frame.Add(DLE); // byte stuffing
            }
            frame.Add(DLE);
            frame.Add(ETX);
            frame.Add(crc[0]);
            frame.Add(crc[1]);

            return frame.ToArray();
        }

        /// <summary>
        /// Extracts the raw payload from a received frame.
        /// Returns null on CRC failure.
        /// </summary>
        public static byte[]? ParseFrame(byte[] frame)
        {
            // Minimum: DLE STX lenH lenL DLE ETX crcH crcL = 8 bytes
            if (frame.Length < 8) return null;

            int idx = 0;
            if (frame[idx++] != DLE || frame[idx++] != STX) return null;

            byte lenH = frame[idx++];
            byte lenL = frame[idx++];
            int dataLen = (lenH << 8) | lenL;

            // Unstuff data
            var data = new List<byte>();
            while (idx < frame.Length - 4) // leave room for DLE ETX CRC CRC
            {
                byte b = frame[idx++];
                if (b == DLE && idx < frame.Length && frame[idx] == DLE)
                {
                    idx++; // consume stuffed DLE
                }
                data.Add(b);
            }

            if (idx + 3 >= frame.Length) return null;
            if (frame[idx++] != DLE || frame[idx++] != ETX) return null;
            byte rcvCrcH = frame[idx++];
            byte rcvCrcL = frame[idx];

            // Verify CRC over: lenH lenL data ETX
            var crcRegion = new List<byte> { lenH, lenL };
            crcRegion.AddRange(data);
            crcRegion.Add(ETX);
            byte[] calcCrc = Crc16.Calculate(crcRegion.ToArray(), 0, crcRegion.Count);

            if (calcCrc[0] != rcvCrcH || calcCrc[1] != rcvCrcL) return null;

            return data.ToArray();
        }

        public static bool IsAck(byte[] buf) =>
            buf.Length >= 2 && buf[0] == DLE && buf[1] == ACK;

        public static bool IsNak(byte[] buf) =>
            buf.Length >= 2 && buf[0] == DLE && buf[1] == NAK;

        public static bool IsEnq(byte[] buf) =>
            buf.Length >= 2 && buf[0] == DLE && buf[1] == ENQ;
    }

    // ─────────────────────────────────────────────────────────────
    //  COMMAND BUILDER
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds command payloads for the F56-BDU per the RS232C enhanced frame format.
    /// Format: [CMD_CLASS] [CMD_CODE] [FF] [00] [00] [PARAM_LEN H L] [PARAMS...]
    /// </summary>
    internal static class CommandBuilder
    {
        private const byte CLASS_CMD = 0x60;
        private const byte CLASS_RSP = 0xE0;

        public static byte[] GetStatus()
        {
            // Command 60 01 FF 00 00 01 00 1C
            return new byte[] { CLASS_CMD, 0x01, 0xFF, 0x00, 0x00, 0x01, 0x00, 0x1C };
        }

        public static byte[] Initialize(
            ushort[] billLengths,   // per cassette (mm * 10), 4 cassettes
            byte[]   billThickness, // per cassette (0.01mm), 4 cassettes
            byte[]   options)
        {
            // Mirrors the F56 Initialize (60 02) command structure
            var param = new List<byte>
            {
                CLASS_CMD, 0x02, 0xFF, 0x00, 0x00,
                0x1A, 0x00  // param length = 26 bytes
            };

            // 4x bill length (2 bytes each)
            foreach (var len in billLengths)
            {
                param.Add((byte)(len >> 8));
                param.Add((byte)(len & 0xFF));
            }

            // 4x thickness (1 byte each)
            param.AddRange(billThickness);

            // padding / options
            for (int i = 0; i < 14; i++)
                param.Add(i < options.Length ? options[i] : (byte)0x00);

            param.Add(0x1C); // end marker
            return param.ToArray();
        }

        /// <summary>
        /// Build a Bill Count / Dispense command.
        /// denominationCounts: array of (denomination value, count) pairs per cassette.
        /// </summary>
        public static byte[] BillCount(
            uint totalAmount,
            (ushort denom, ushort count)[] cassetteCounts)
        {
            var param = new List<byte>
            {
                CLASS_CMD, 0x03, 0xFF, 0x00, 0x00
            };

            // Encode total amount as BCD / hex (4 bytes)
            param.Add((byte)((totalAmount >> 24) & 0xFF));
            param.Add((byte)((totalAmount >> 16) & 0xFF));
            param.Add((byte)((totalAmount >>  8) & 0xFF));
            param.Add((byte)(totalAmount & 0xFF));

            // Per-cassette count info (4 cassettes × 4 bytes)
            foreach (var (denom, count) in cassetteCounts)
            {
                param.Add((byte)(denom >> 8));
                param.Add((byte)(denom & 0xFF));
                param.Add((byte)(count >> 8));
                param.Add((byte)(count & 0xFF));
            }

            // Pad remaining bytes
            while (param.Count < 52) param.Add(0x00);
            param.Add(0x1C);

            return param.ToArray();
        }

        public static byte[] Reset()
        {
            return new byte[] { CLASS_CMD, 0x08, 0xFF, 0x00, 0x00, 0x00, 0x1C };
        }

        public static byte[] Present()
        {
            return new byte[] { CLASS_CMD, 0x09, 0xFF, 0x00, 0x00, 0x00, 0x1C };
        }

        public static byte[] Retract()
        {
            return new byte[] { CLASS_CMD, 0x0A, 0xFF, 0x00, 0x00, 0x00, 0x1C };
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  MAIN BDU DRIVER
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Main driver for the Fujitsu F53/F56 BDU (Bill Dispensing Unit).
    /// Handles RS232C communication, framing, retries and error interpretation.
    /// </summary>
    public class BduDriver : IDisposable
    {
        private readonly SerialPort _port;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _disposed;

        // Protocol timers (milliseconds) – per F56-BDU RS232C spec
        private const int TIMEOUT_ENQ_ACK   = 5000;
        private const int TIMEOUT_ACK_STX   = 5000;
        private const int TIMEOUT_DATA_GAP  = 5000;
        private const int TIMEOUT_CRC_ACK   = 5000;
        private const int MAX_RETRIES        = 3;

        // ── Constructor ──────────────────────────────────────────

        /// <param name="portName">E.g. "COM3" or "/dev/ttyS0"</param>
        public BduDriver(string portName)
        {
            _port = new SerialPort(portName)
            {
                BaudRate  = 9600,
                DataBits  = 8,
                Parity    = Parity.Even,
                StopBits  = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout  = TIMEOUT_DATA_GAP,
                WriteTimeout = 2000,
            };
        }

        /// <param name="portName">Serial port name</param>
        /// <param name="baudRate">Override baud rate (default 9600)</param>
        public BduDriver(string portName, int baudRate) : this(portName)
        {
            _port.BaudRate = baudRate;
        }

        // ── Connection ───────────────────────────────────────────

        public void Open()
        {
            if (!_port.IsOpen)
                _port.Open();
        }

        public void Close()
        {
            if (_port.IsOpen)
                _port.Close();
        }

        public bool IsOpen => _port.IsOpen;

        // ── High-level API ───────────────────────────────────────

        /// <summary>Reads the current device status.</summary>
        public async Task<StatusResponse> GetStatusAsync(CancellationToken ct = default)
        {
            byte[] payload = CommandBuilder.GetStatus();
            var result = await SendCommandAsync(payload, ct);

            if (!result.Success)
                return new StatusResponse
                {
                    Status = DeviceStatus.Error,
                    Description = result.ErrorMessage
                };

            return ParseStatusResponse(result.RawData);
        }

        /// <summary>
        /// Initializes the device with bill parameters.
        /// billLengths: array of 4 lengths in tenths of mm (e.g. 0xAF8C = 44940 = ~160mm).
        /// billThickness: array of 4 thickness values (e.g. 0x0C).
        /// </summary>
        public async Task<BduResponse> InitializeAsync(
            ushort[] billLengths,
            byte[]   billThickness,
            CancellationToken ct = default)
        {
            if (billLengths.Length < 4)
                throw new ArgumentException("Must provide 4 bill length values.", nameof(billLengths));
            if (billThickness.Length < 4)
                throw new ArgumentException("Must provide 4 bill thickness values.", nameof(billThickness));

            byte[] payload = CommandBuilder.Initialize(billLengths, billThickness, Array.Empty<byte>());
            return await SendCommandAsync(payload, ct);
        }

        /// <summary>
        /// Dispenses bills.
        /// totalAmount: total value to dispense.
        /// cassetteCounts: per-cassette (denomination, count) pairs (up to 4).
        /// </summary>
        public async Task<DispenseResult> DispenseAsync(
            uint totalAmount,
            (ushort denom, ushort count)[] cassetteCounts,
            CancellationToken ct = default)
        {
            if (cassetteCounts.Length < 4)
            {
                var padded = new (ushort, ushort)[4];
                cassetteCounts.CopyTo(padded, 0);
                cassetteCounts = padded;
            }

            byte[] payload = CommandBuilder.BillCount(totalAmount, cassetteCounts);
            var result = await SendCommandAsync(payload, ct);

            if (!result.Success)
                return new DispenseResult
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage,
                    ErrorCode = result.FwErrorCode.Length >= 2
                        ? $"{result.FwErrorCode[0]:X2}{result.FwErrorCode[1]:X2}"
                        : string.Empty
                };

            // Parse dispensed count from response (byte offset depends on response format)
            int dispensed = ParseDispensedCount(result.RawData);
            return new DispenseResult { Success = true, NotesDispensed = dispensed };
        }

        /// <summary>Sends a mechanical reset command.</summary>
        public async Task<BduResponse> ResetAsync(CancellationToken ct = default)
        {
            return await SendCommandAsync(CommandBuilder.Reset(), ct);
        }

        /// <summary>Presents notes at the ejection slot.</summary>
        public async Task<BduResponse> PresentAsync(CancellationToken ct = default)
        {
            return await SendCommandAsync(CommandBuilder.Present(), ct);
        }

        /// <summary>Retracts notes back into the device.</summary>
        public async Task<BduResponse> RetractAsync(CancellationToken ct = default)
        {
            return await SendCommandAsync(CommandBuilder.Retract(), ct);
        }

        // ── Low-level communication ──────────────────────────────

        /// <summary>
        /// Executes the full DLE-ENQ handshake + frame send + receive sequence.
        /// Retries up to MAX_RETRIES times on NAK or timeout.
        /// </summary>
        private async Task<BduResponse> SendCommandAsync(byte[] payload, CancellationToken ct)
        {
            await _lock.WaitAsync(ct);
            try
            {
                for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        // Step 1: Send ENQ, wait for ACK
                        _port.Write(FrameCodec.BuildEnq(), 0, 2);
                        byte[] enqResp = await ReadBytesAsync(2, TIMEOUT_ENQ_ACK, ct);

                        if (!FrameCodec.IsAck(enqResp))
                        {
                            await Task.Delay(500, ct); // wait before retry
                            continue;
                        }

                        // Step 2: Send framed command
                        byte[] frame = FrameCodec.BuildFrame(payload);
                        _port.Write(frame, 0, frame.Length);

                        // Step 3: Wait for ACK/NAK on transmitted frame
                        byte[] txAck = await ReadBytesAsync(2, TIMEOUT_CRC_ACK, ct);
                        if (FrameCodec.IsNak(txAck))
                        {
                            // Remote NAK – retry
                            await Task.Delay(200, ct);
                            continue;
                        }

                        // Step 4: Wait for device's ENQ (device sending response)
                        byte[] devEnq = await ReadBytesAsync(2, TIMEOUT_ACK_STX, ct);
                        if (!FrameCodec.IsEnq(devEnq))
                            continue;

                        // Send ACK to allow device to send
                        _port.Write(FrameCodec.BuildAck(), 0, 2);

                        // Step 5: Receive response frame
                        byte[]? responseData = await ReceiveFrameAsync(ct);
                        if (responseData == null)
                        {
                            _port.Write(FrameCodec.BuildNak(), 0, 2);
                            continue;
                        }

                        // ACK the response
                        _port.Write(FrameCodec.BuildAck(), 0, 2);

                        // Check for error in response
                        if (responseData.Length >= 6 && responseData[5] != 0x00)
                        {
                            byte errByte1 = responseData.Length > 6 ? responseData[6] : (byte)0;
                            byte errByte2 = responseData.Length > 7 ? responseData[7] : (byte)0;
                            string errDesc = BduErrorInterpreter.Interpret(errByte1, errByte2);
                            return BduResponse.Fail(errDesc, new[] { errByte1, errByte2 });
                        }

                        return BduResponse.Ok(responseData);
                    }
                    catch (TimeoutException)
                    {
                        if (attempt == MAX_RETRIES - 1)
                            return BduResponse.Fail("Communication timeout after max retries");
                    }
                }

                return BduResponse.Fail("Failed after maximum retries");
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Reads a complete DLE-framed response from the port.</summary>
        private async Task<byte[]?> ReceiveFrameAsync(CancellationToken ct)
        {
            var buffer = new List<byte>();
            var deadline = DateTime.UtcNow.AddMilliseconds(TIMEOUT_DATA_GAP);

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                if (_port.BytesToRead > 0)
                {
                    buffer.Add((byte)_port.ReadByte());

                    // Check for end-of-frame (DLE ETX + 2 CRC bytes)
                    if (buffer.Count >= 8 && IsFrameComplete(buffer))
                        return FrameCodec.ParseFrame(buffer.ToArray());
                }
                else
                {
                    await Task.Delay(10, ct);
                }
            }
            return null;
        }

        private static bool IsFrameComplete(List<byte> buf)
        {
            // A frame ends with DLE ETX crcH crcL
            // We look for DLE(0x10) ETX(0x03) pattern not preceded by another DLE
            for (int i = buf.Count - 4; i >= 2; i--)
            {
                if (buf[i] == 0x10 && buf[i + 1] == 0x03)
                    return true;
            }
            return false;
        }

        private async Task<byte[]> ReadBytesAsync(int count, int timeoutMs, CancellationToken ct)
        {
            var result = new byte[count];
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            int idx = 0;

            while (idx < count)
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException($"Timeout waiting for {count} bytes");

                if (_port.BytesToRead > 0)
                    result[idx++] = (byte)_port.ReadByte();
                else
                    await Task.Delay(5, ct);
            }
            return result;
        }

        // ── Response parsers ─────────────────────────────────────

        private static StatusResponse ParseStatusResponse(byte[] data)
        {
            if (data.Length < 10)
                return new StatusResponse { Status = DeviceStatus.Unknown, Description = "Response too short" };

            // POM byte at offset 6 (based on F56 command trace analysis)
            byte pom = data.Length > 6 ? data[6] : (byte)0;

            // Sensor register at offset ~14
            byte sensorH = data.Length > 14 ? data[14] : (byte)0;
            byte sensorL = data.Length > 15 ? data[15] : (byte)0;

            // Cassette register at offset 7 (4 bytes)
            bool cas1 = data.Length > 7  && (data[7]  & 0x01) != 0;
            bool cas2 = data.Length > 8  && (data[8]  & 0x01) != 0;
            bool cas3 = data.Length > 9  && (data[9]  & 0x01) != 0;
            bool cas4 = data.Length > 10 && (data[10] & 0x01) != 0;

            bool mediaEjection = (sensorH & 0x08) != 0;  // EJSF bit
            bool mediaPool     = (sensorH & 0x04) != 0;  // BPS bit
            bool shutterOpen   = (sensorL & 0x02) != 0;  // SOSF bit
            bool rejectFull    = (pom & 0x04) != 0;

            DeviceStatus status;
            if (rejectFull)         status = DeviceStatus.Error;
            else if (!cas1 && !cas2 && !cas3 && !cas4)
                                    status = DeviceStatus.NoCassette;
            else if (mediaEjection) status = DeviceStatus.MediaRemaining;
            else                    status = DeviceStatus.Ready;

            return new StatusResponse
            {
                Status           = status,
                RawStatus        = data,
                Description      = $"POM=0x{pom:X2}, Sensors=0x{sensorH:X2}{sensorL:X2}",
                Cassette1Present = cas1,
                Cassette2Present = cas2,
                Cassette3Present = cas3,
                Cassette4Present = cas4,
                MediaAtEjection  = mediaEjection,
                MediaAtPool      = mediaPool,
                ShutterOpen      = shutterOpen,
                RejectBoxFull    = rejectFull,
            };
        }

        private static int ParseDispensedCount(byte[] data)
        {
            // The bill count response includes counts per cassette
            // This is a simplified extraction; exact offsets depend on firmware version
            if (data.Length < 30) return 0;
            int total = 0;
            for (int i = 20; i < Math.Min(28, data.Length - 1); i += 2)
                total += (data[i] << 8) | data[i + 1];
            return total;
        }

        // ── IDisposable ──────────────────────────────────────────

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Close();
                _port.Dispose();
                _lock.Dispose();
            }
        }
    }
}
