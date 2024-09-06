# grbltest

**grbltest** is a .NET console application developed in C# that communicates with GRBL firmware via a serial connection to control CNC machines. It supports jogging, homing, resetting alarms, and querying GRBL status through keyboard inputs.

**Note**: This is a work-in-progress project and is not production-ready.

## Prerequisites

- **Visual Studio 2022** or later
- **.NET 8.0 SDK** or later
- **NuGet package**: `System.IO.Ports`
- Serial (COM) port connected to a GRBL format compliant controller (e.g. openbuilds blackbox x32)

## Installation and Setup

1. Clone the repository to your local machine.
2. Open the project in **Visual Studio 2022**.
3. Install the required NuGet package `System.IO.Ports`. You can do this via the **NuGet Package Manager** or run the following command in the **Package Manager Console**:

    ```
    Install-Package System.IO.Ports
    ```

4. **Note**: The COM port is hardcoded to `COM3` in the application. You may need to change this based on your setup.

## How to Run

1. Open the solution in **Visual Studio 2022**.
2. Build the solution (`Ctrl + Shift + B`).
3. Run the project (`F5`).

## Features

- **Jogging**: Control the CNC machine using arrow keys (`←`, `→`, `↑`, `↓`).
- **Homing**: Trigger the homing cycle using the `H` key.
- **Reset Alarms**: Reset GRBL's alarm state using the `X` key.
- **Go to Center**: Move the machine to the center of the defined work area (`X=380`, `Y=130`) using the `C` key.
- **Query Status**: Send the `?` command to GRBL to query the machine's current status using the `S` key.
- **Exit**: Quit the application using the `Q` key.

## Key Bindings

| Key             | Action                                 |
|-----------------|----------------------------------------|
| `←` (Left Arrow)  | Jog machine left (X-axis -10 mm)       |
| `→` (Right Arrow) | Jog machine right (X-axis +10 mm)      |
| `↑` (Up Arrow)    | Jog machine up (Y-axis +10 mm)         |
| `↓` (Down Arrow)  | Jog machine down (Y-axis -10 mm)       |
| `H`             | Trigger homing cycle                   |
| `X`             | Reset alarms                          |
| `C`             | Move to center of work area            |
| `S`             | Query GRBL status (`?` command)        |
| `Q`             | Exit the application                   |

## Known Issues

- **Command Piling**: Jog commands may pile up if keys are held down too long. This has been mitigated by adding a debounce mechanism with a delay of 200ms.
- **COM Port**: The COM port is hardcoded to `COM3`. Users need to change this manually if their machine is connected to a different port.
- **Work in Progress**: The project is still in development and is not production-ready.

## License

This project is licensed under the MIT License.

## Copyright

Copyright (c) 2024 Newmatik GmbH  
Am Markt 1, 55619 Hennweiler, Germany  
software@newmatik.com, www.newmatik.com

