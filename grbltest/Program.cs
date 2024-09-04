using System;
using System.IO.Ports;
using System.Threading;

class Program
{
    // Define constants
    const int jogSpeed = 10000; // Jog speed constant
    const int rapidSpeed = 20000; // Rapid move speed for go-to-location
    const int debounceInterval = 200; // Debounce time in milliseconds to avoid piling up commands
    const int workarea_x = 790; // Work area for X-axis
    const int workarea_y = 260; // Work area for Y-axis

    static SerialPort serialPort;
    static bool isBusy = false; // To track if the system is currently processing a command
    static DateTime lastCommandTime = DateTime.Now; // To track the last command time for debounce

    static void Main(string[] args)
    {
        // Set up the serial port configuration
        serialPort = new SerialPort("COM3", 115200)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 500,
            WriteTimeout = 500
        };

        // Try to open the port with retry logic
        while (true)
        {
            try
            {
                // Attempt to open the serial port
                serialPort.Open();
                Console.WriteLine("Connected to GRBL on COM3.");

                // Soft reset the system to clear any stuck state
                SoftReset();

                // Disable soft limits temporarily
                DisableSoftLimits();

                // Attempt to reset alarms
                ResetAlarms();

                // Start capturing jog commands from keyboard
                CaptureJogCommands();

                // Exit the loop after successful connection and execution
                break;
            }
            catch (UnauthorizedAccessException)
            {
                // Handle COM port access denied error
                Console.WriteLine("Error: Access to COM3 is denied. Do you want to retry? (Y/N)");

                // Ask the user if they want to retry
                var input = Console.ReadKey(true).Key;
                if (input == ConsoleKey.Y)
                {
                    Console.WriteLine("Retrying...");
                    Thread.Sleep(1000); // Optional: Wait a bit before retrying
                    continue; // Retry connecting to COM3
                }
                else
                {
                    Console.WriteLine("Exiting program.");
                    return; // Exit the program if the user doesn't want to retry
                }
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                Console.WriteLine("Error: " + ex.Message);
                return; // Exit if an unknown error occurs
            }
        }

        // Ensure serial port is closed when the program finishes
        if (serialPort.IsOpen)
            serialPort.Close();
    }

    // Soft Reset (Ctrl+X)
    static void SoftReset()
    {
        Console.WriteLine("Performing soft reset (Ctrl+X)...");
        serialPort.Write(new byte[] { 0x18 }, 0, 1); // Sends Ctrl + X (Soft Reset)
        Thread.Sleep(500);
        string status = GetGrblStatus();
        Console.WriteLine($"System status after soft reset: {status}");
    }

    // Disable Soft Limits Temporarily
    static void DisableSoftLimits()
    {
        SendCommand("$20=0", "Disabling soft limits temporarily");
        Thread.Sleep(500); // Wait for confirmation
    }

    // Reset alarms with $X command
    static void ResetAlarms()
    {
        Console.WriteLine("Attempting to reset alarms...");
        SendCommand("$X", "Resetting alarms");

        // Wait briefly before checking the status
        Thread.Sleep(500);

        string status = GetGrblStatus();
        if (status.Contains("Idle"))
        {
            Console.WriteLine("Alarm reset successfully. System is now idle.");
        }
        else if (status.Contains("Alarm"))
        {
            Console.WriteLine("Failed to reset alarms. System is still in an alarm state.");
        }
        else
        {
            Console.WriteLine("Unexpected system status after attempting to reset alarms: " + status);
        }
    }

    // Initialize GRBL by homing the system
    static void ManualHome()
    {
        SendCommand("$H", "Starting homing procedure");
        WaitForIdle(); // Wait for homing to complete
    }

    // Move the machine to the center of the work area
    static void GoToCenter()
    {
        string command = $"$J=G90 G21 X{workarea_x / 2} Y{workarea_y / 2} F{rapidSpeed}";
        SendCommand(command, "Moving to center location (X=380, Y=130)");
        WaitForIdle();
    }

    // Capture jog commands and additional key inputs
    static void CaptureJogCommands()
    {
        Console.WriteLine("Use the arrow keys to jog the machine, 'S' for status, 'H' to home, 'X' to reset alarms, 'C' to move to center, 'Q' to quit.");

        while (true)
        {
            // Check for keypress without flooding inputs
            if (Console.KeyAvailable && !isBusy)
            {
                ConsoleKey key = Console.ReadKey(true).Key;

                // Debounce check: only process new command if debounce interval has passed
                if ((DateTime.Now - lastCommandTime).TotalMilliseconds > debounceInterval)
                {
                    // Send jog commands based on arrow keys or perform other actions
                    switch (key)
                    {
                        case ConsoleKey.LeftArrow:
                            CheckAndSendJogCommand($"G91 G0 X-10 F{jogSpeed}"); // Jog left (-X direction)
                            break;
                        case ConsoleKey.RightArrow:
                            CheckAndSendJogCommand($"G91 G0 X10 F{jogSpeed}");  // Jog right (+X direction)
                            break;
                        case ConsoleKey.UpArrow:
                            CheckAndSendJogCommand($"G91 G0 Y10 F{jogSpeed}");  // Jog up (+Y direction)
                            break;
                        case ConsoleKey.DownArrow:
                            CheckAndSendJogCommand($"G91 G0 Y-10 F{jogSpeed}"); // Jog down (-Y direction)
                            break;
                        case ConsoleKey.H:
                            ManualHome(); // Manually trigger homing
                            break;
                        case ConsoleKey.X:
                            ResetAlarms(); // Reset alarms manually
                            break;
                        case ConsoleKey.C:
                            GoToCenter(); // Go to the center of the defined work area
                            break;
                        case ConsoleKey.S:
                            SendStatusQuery(); // Send the '?' command
                            break;
                        case ConsoleKey.Q:
                            // Quit the program
                            Console.WriteLine("Exiting...");
                            Environment.Exit(0);
                            break;
                        default:
                            Console.WriteLine("Invalid key. Use arrow keys, 'S' for status, 'H' for homing, 'C' for center, 'X' to reset alarms, or 'Q' to quit.");
                            break;
                    }

                    // Update the last command time to enforce debounce
                    lastCommandTime = DateTime.Now;
                }
            }
        }
    }

    // Send jog command if system is idle
    static void CheckAndSendJogCommand(string jogCommand)
    {
        string status = GetGrblStatus();
        if (status.Contains("Idle"))
        {
            SendCommand(jogCommand, "Jog command");
        }
        else if (status.Contains("Alarm"))
        {
            Console.WriteLine("System is in an alarm state. Reset required.");
        }
        else
        {
            Console.WriteLine($"System is busy ({status}). Jog command not sent.");
        }
    }

    // Send the "?" command to get GRBL status and print the response
    static void SendStatusQuery()
    {
        SendCommand("?", "Status query");
    }

    // Send the command to GRBL and print the command sent
    static void SendCommand(string command, string description = "")
    {
        if (serialPort.IsOpen)
        {
            isBusy = true; // Set busy flag when a command is sent
            serialPort.WriteLine(command);
            Console.WriteLine($"Sent command: {command} - {description}");

            // Wait for the system to become idle after each command
            WaitForIdle();
        }
        else
        {
            Console.WriteLine("Serial port is not open.");
        }
    }

    // Get the current status of GRBL using the '?' command
    static string GetGrblStatus()
    {
        // Send a '?' command to get the current status of GRBL
        serialPort.Write("?");
        Thread.Sleep(100); // Give GRBL some time to respond

        try
        {
            string response = serialPort.ReadExisting();
            if (string.IsNullOrEmpty(response))
            {
                Console.WriteLine("Empty GRBL status received, continuing to wait...");
                return ""; // Continue checking in case of an empty response
            }
            Console.WriteLine($"Received GRBL Status: {response}");
            return response;
        }
        catch (TimeoutException)
        {
            Console.WriteLine("Status query timed out.");
            return "Unknown";
        }
    }

    // Wait until the GRBL system becomes idle after homing or resetting alarms
    static void WaitForIdle()
    {
        int maxRetries = 20; // Max number of retries (for a total of 10 seconds)
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            string status = GetGrblStatus();
            if (status.Contains("Idle"))
            {
                Console.WriteLine("System is now idle.");
                isBusy = false; // Reset busy flag once the system is idle
                break;
            }
            else if (status.Contains("Alarm"))
            {
                Console.WriteLine("System is in an alarm state. Please reset alarms.");
                isBusy = false; // Reset busy flag in case of alarm
                break;
            }
            else if (string.IsNullOrEmpty(status))
            {
                Console.WriteLine("System is still homing, waiting for Idle status...");
            }

            retryCount++;
            Thread.Sleep(500); // Check status every 500 ms
        }

        if (retryCount == maxRetries)
        {
            Console.WriteLine("Timed out while waiting for the system to become idle.");
            isBusy = false; // Reset busy flag after timeout
        }
    }
}
