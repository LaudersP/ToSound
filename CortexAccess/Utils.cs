using System;

namespace CortexAccess
{
    public class Utils
    {
        public static Int64 GetEpochTimeNow()
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            Int64 timeSinceEpoch = (Int64)t.TotalMilliseconds;
            return timeSinceEpoch;

        }
        public static string GenerateUuidProfileName(string prefix)
        {
            return prefix + "-" + GetEpochTimeNow();
        }

        // Print a colored message
        public void SendColoredMessage(string messageType, ConsoleColor typeColor, string messageContent, bool newLine)
        {
            Console.ForegroundColor = typeColor;
            Console.Write($"[{messageType}] ");
            Console.ForegroundColor = ConsoleColor.White;

            if (newLine)
            {
                Console.WriteLine(messageContent);
            }
            else
            {
                Console.Write(messageContent);
            }

        }

        // Print a error message
        public void SendErrorMessage(string message, bool newLine = true)
        {
            SendColoredMessage("ERROR", ConsoleColor.Red, message, newLine);
        }

        // Print a success message
        public void SendSuccessMessage(string message, bool newLine = true)
        {
            SendColoredMessage("SUCCESS", ConsoleColor.Green, message, newLine);
        }

        // Print a warning message
        public void SendWarningMessage(string message, bool newLine = true)
        {
            SendColoredMessage("WARNING", ConsoleColor.DarkYellow, message, newLine);
        }
    }
}
