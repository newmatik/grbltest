using grbltest;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

class Program
{
    // Define constants
    const int debounceInterval = 200; // Debounce time in milliseconds to avoid piling up commands

    static Config? configTemp;
    static SerialPort serialPort;
    static bool isBusy = false; // To track if the system is currently processing a command
    static DateTime lastCommandTime = DateTime.Now; // To track the last command time for debounce

    static void Main(string[] args)
    {
        configTemp = Config.LoadConfig("config.json");
        if (configTemp == null)
        {
            Log.Logging("Couldn't read config-file", Log.LogLevel.Error);
            return;
        }
        var port = GetCOMPortFromUser();
        if(!string.IsNullOrWhiteSpace(port))
            configTemp.ComPort = port.StartsWith("COM") ? port : $"COM{port}";

        // Set up the serial port configuration
        serialPort = new SerialPort(configTemp.ComPort, 115200)
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
                Log.Logging($"Connected to GRBL on {port}.", Log.LogLevel.Info);

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
                Log.Logging($"Access to {configTemp.ComPort} is denied. Do you want to retry? (Y/N)", Log.LogLevel.Warning);

                // Ask the user if they want to retry
                var input = Console.ReadKey(true).Key;
                if (input == ConsoleKey.Y)
                {
                    Log.Logging("Retrying...", Log.LogLevel.Info);
                    Thread.Sleep(1000); // Optional: Wait a bit before retrying
                    continue; // Retry connecting to COM3
                }
                else
                {
                    Log.Logging("Exiting program.", Log.LogLevel.Info);
                    return; // Exit the program if the user doesn't want to retry
                }
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                Log.Logging($"{ex.Message}", Log.LogLevel.Info);
                return; // Exit if an unknown error occurs
            }
            finally
            {
                // Ensure serial port is closed when the program finishes
                if (serialPort.IsOpen)
                    serialPort.Close();
            }
        }

        // Ensure serial port is closed when the program finishes
        if (serialPort.IsOpen)
            serialPort.Close();
    }
    static string GetCOMPortFromUser()
    {
        Log.Logging("COM port to use (e.g., COM3 or just 3 or empty): ", Log.LogLevel.Info);
        return Console.ReadLine();
    }

    // Soft Reset (Ctrl+X)
    static void SoftReset()
    {
        Log.Logging("Performing soft reset (Ctrl+X)...", Log.LogLevel.Info);
        serialPort.Write(new byte[] { 0x18 }, 0, 1); // Sends Ctrl + X (Soft Reset)
        Thread.Sleep(500);
        //StartStatusMonitoring();
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
        Log.Logging("Attempting to reset alarms...", Log.LogLevel.Info);
        SendCommand("$X", "Resetting alarms");
    }

    // Initialize GRBL by homing the system
    static void ManualHome()
    {
        SendCommand("$H", "Starting homing procedure");
    }

    // Move the machine to the center of the work area
    static void GoToCenter()
    {
        int x = configTemp.WorkAreaX / 2;
        int y = configTemp.WorkAreaY / 2;
        string command = $"$J=G90 G21 X{x} Y{y} F{configTemp.RapidSpeed}";
        SendCommand(command, $"Moving to center location (X={x}, Y={y})");
    }

    // Capture jog commands and additional key inputs
    static void CaptureJogCommands()
    {
        Log.Logging("Use the arrow keys to jog the machine, 'S' for status, 'H' to home, 'X' to reset alarms, 'C' to move to center, 'Q' to quit.", Log.LogLevel.Info);

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
                            CheckAndSendJogCommand($"G91 G0 X-10 F{configTemp.JogSpeed}"); // Jog left (-X direction)
                            break;
                        case ConsoleKey.RightArrow:
                            CheckAndSendJogCommand($"G91 G0 X10 F{configTemp.JogSpeed}");  // Jog right (+X direction)
                            break;
                        case ConsoleKey.UpArrow:
                            CheckAndSendJogCommand($"G91 G0 Y10 F{configTemp.JogSpeed}");  // Jog up (+Y direction)
                            break;
                        case ConsoleKey.DownArrow:
                            CheckAndSendJogCommand($"G91 G0 Y-10 F{configTemp.JogSpeed}"); // Jog down (-Y direction)
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
                            Log.Logging("Exiting...", Log.LogLevel.Info);
                            Environment.Exit(0);
                            break;
                        default:
                            Log.Logging("Invalid key. Use arrow keys, 'S' for status, 'H' for homing, 'C' for center, 'X' to reset alarms, or 'Q' to quit.", Log.LogLevel.Warning);
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
            Log.Logging("System is in an alarm state. Reset required.", Log.LogLevel.Warning);
        }
        else
        {
            Log.Logging($"System is busy ({status}). Jog command not sent.", Log.LogLevel.Warning);
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
        SendCommandWithRetry(3, command, description);
    }
    static void SendCommandWithRetry(int retryCount, string command, string description = "")
    {
        int attempts = 0;
        while (attempts < retryCount)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    isBusy = true; // Set busy flag when a command is sent
                    serialPort.WriteLine(command);
                    Log.Logging($"Sent command: {command} - {description}", Log.LogLevel.Info);

                    // Wait for the system to become idle after each command
                    WaitForIdle();
                }
                else
                {
                    Log.Logging("Serial port is not open.", Log.LogLevel.Error);
                }
                return; // Exit if the command succeeds
            }
            catch (Exception ex)
            {
                attempts++;
                Log.Logging($"Failed to send command '{command}'. Attempt {attempts}/{retryCount}. Error: {ex.Message}", Log.LogLevel.Error);
                Thread.Sleep(500); // Wait before retrying
            }
        }
        Log.Logging($"Failed to execute command '{command}' after {retryCount} attempts.", Log.LogLevel.Error);
    }

    // Get the current status of GRBL using the '?' command
    static string GetGrblStatus()
    {
        // Send a '?' command to get the current status of GRBL
        // Important: Don't use SendCommand() here to avoid infinite loops
        serialPort.Write("?");
        Thread.Sleep(100); // Give GRBL some time to respond

        try
        {
            string response = serialPort.ReadExisting();
            if (string.IsNullOrEmpty(response))
            {
                Log.Logging("Empty GRBL status received, continuing to wait...", Log.LogLevel.Warning);
                return ""; // Continue checking in case of an empty response
            }
            Log.Logging($"Received GRBL Status: {response}", Log.LogLevel.Info);
            return response;
        }
        catch (TimeoutException)
        {
            Log.Logging("Status query timed out.", Log.LogLevel.Error);
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
                Log.Logging("System is now idle.", Log.LogLevel.Info);
                isBusy = false; // Reset busy flag once the system is idle
                break;
            }
            else if (status.Contains("Alarm"))
            {
                Log.Logging("System is in an alarm state. Please reset alarms.", Log.LogLevel.Warning);
                isBusy = false; // Reset busy flag in case of alarm
                break;
            }
            else if (string.IsNullOrEmpty(status))
            {
                Log.Logging("System is still homing, waiting for Idle status...", Log.LogLevel.Warning);
            }

            retryCount++;
            Thread.Sleep(500); // Check status every 500 ms
        }

        if (retryCount == maxRetries)
        {
            Log.Logging("Timed out while waiting for the system to become idle.", Log.LogLevel.Error);
            isBusy = false; // Reset busy flag after timeout
        }
    }
    static void StartStatusMonitoring()
    {
        new Thread(() =>
        {
            while (true)
            {
                string status = GetGrblStatus();
                //Trace.WriteLine($"GRBL Status: {status}");
                Thread.Sleep(1000); // Check status every second
            }
        }).Start();
    }
}
