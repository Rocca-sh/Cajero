# Fujitsu F53/F56 BDU SDK — C# / .NET 8

SDK para controlar la unidad dispensadora de billetes Fujitsu F53/F56 (BDU)
via RS232C. Basado en la documentación oficial:

- `W1KD03234-0001` — Spare Parts Catalog  
- `A3KD03234-0002` — RS232C Interface Specifications  
- `K2KD03234-0001` — RAS Specifications  
- `K3KD03234-0001` — Error Code List  
- `A2KD41003-3811` — SP Error Code List (GBDU)

---

## Estructura del proyecto

```
FujitsuBDU/
├── FujitsuBDU.cs      ← SDK completo (driver + clases)
├── Program.cs         ← Ejemplo de uso
├── FujitsuBDU.csproj
└── README.md
```

---

## Protocolo RS232C

| Parámetro          | Valor         |
|--------------------|---------------|
| Velocidad          | 9600 bps      |
| Bits de datos      | 8             |
| Paridad            | Par (Even)    |
| Bits de parada     | 1             |
| Flujo              | Ninguno       |
| Modo               | Full duplex   |
| CRC                | CRC-16/CCITT  |

### Estructura de trama

```
DLE STX | LEN_H LEN_L | DATA... | DLE ETX | CRC_H CRC_L
```

El CRC se calcula sobre: `LEN_H LEN_L DATA... ETX`

### Secuencia de transmisión

```
PC  → BDU:  DLE ENQ
BDU → PC:   DLE ACK
PC  → BDU:  [TRAMA DE COMANDO]
BDU → PC:   DLE ACK  (o DLE NAK → reintentar)
BDU → PC:   DLE ENQ
PC  → BDU:  DLE ACK
BDU → PC:   [TRAMA DE RESPUESTA]
PC  → BDU:  DLE ACK
```

---

## Uso rápido

```csharp
using FujitsuBDU;

using var bdu = new BduDriver("COM3");
bdu.Open();

// Leer estado
var status = await bdu.GetStatusAsync();
Console.WriteLine(status.Status); // Ready / Error / NoCassette / etc.

// Inicializar
await bdu.InitializeAsync(
    billLengths:   new ushort[] { 0xAF8C, 0xAF8C, 0xAF8C, 0xAF8C },
    billThickness: new byte[]   { 0x0C,   0x0C,   0x0C,   0x0C   }
);

// Dispensar: 5 billetes de $100 del cassette 1
var result = await bdu.DispenseAsync(
    totalAmount: 500,
    cassetteCounts: new[]
    {
        ((ushort)100, (ushort)5),
        ((ushort)0,   (ushort)0),
        ((ushort)0,   (ushort)0),
        ((ushort)0,   (ushort)0),
    }
);

if (result.Success)
    await bdu.PresentAsync();
else
    Console.WriteLine(result.ErrorMessage);

// Interpretar un código de error
string desc = BduErrorInterpreter.Interpret(0x78, 0x01); // "JAM at FDLS1"
string spDesc = BduErrorInterpreter.InterpretSpError("$$0101"); // Media remaining...
```

---

## Clases principales

### `BduDriver`
| Método | Descripción |
|---|---|
| `Open()` / `Close()` | Abre/cierra el puerto serial |
| `GetStatusAsync()` | Lee el estado del dispositivo |
| `InitializeAsync(lengths, thickness)` | Inicializa con parámetros de billetes |
| `DispenseAsync(amount, counts)` | Dispensa billetes |
| `PresentAsync()` | Presenta billetes en la ranura |
| `RetractAsync()` | Retrae los billetes al interior |
| `ResetAsync()` | Reset mecánico |

### `BduErrorInterpreter`
| Método | Descripción |
|---|---|
| `Interpret(byte, byte)` | Interpreta código FW (semimajor + additional) |
| `InterpretSpError(string)` | Interpreta código SP (e.g. "$$0101") |

### `StatusResponse`
```
Status           → DeviceStatus enum
Cassette1–4Present → bool
MediaAtEjection  → bool
MediaAtPool      → bool
ShutterOpen      → bool
RejectBoxFull    → bool
```

---

## Códigos de error FW (semimajor byte)

| Rango | Sección |
|---|---|
| 0x10–0x1F | Cassette 1/5 |
| 0x20–0x2F | Cassette 2/6 |
| 0x30–0x3F | Cassette 3/7 |
| 0x40–0x4F | Cassette 4/8 |
| 0x50–0x5F | Pool de billetes |
| 0x70–0x7F | Sección de transferencia / JAM |
| 0x80–0x8F | Verificación de billetes |
| 0xA0–0xAF | Expulsión / Shutter |
| 0xB0–0xBF | Caja de rechazo / captura |
| 0xC0–0xCF | Descarga de firmware |
| 0xE0–0xEF | Errores D-level |
| 0xF0–0xFF | Controlador BD |

---

## Compilar y ejecutar

```bash
cd FujitsuBDU
dotnet build
dotnet run -- COM3
```

---

## Notas de integración

1. El byte stuffing de DLE está implementado en `FrameCodec.BuildFrame()`.
2. El timeout por defecto es **5 segundos** (configurable en `BduDriver`).
3. Se realizan hasta **3 reintentos** automáticos en caso de NAK o timeout.
4. Usar `CancellationToken` para cancelación en producción.
5. `BduDriver` implementa `IDisposable` — usar con `using`.
