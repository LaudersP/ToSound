using CoreOSC;
using CoreOSC.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CortexAccess
{
    public class OSC
    {
        private UdpClient _udpClient;
        private readonly string _ipAddress;
        private readonly int _portNum;
        private readonly Utils _utilities = new Utils();

        public OSC(string ipAddress, int portNum)
        {
            _ipAddress = ipAddress;
            _portNum = portNum;

            // Create the instance of the client for sending
            _udpClient = new UdpClient(_ipAddress, _portNum);

            _utilities.SendSuccessMessage("Sending data on [", false);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(_portNum);
            Console.ForegroundColor= ConsoleColor.White;
            Console.WriteLine("]");
        }

        public void SendMessage(string address, params object[] arguments)
        {
            try
            {
                // Construct the OSC message
                var message = new OscMessage(new Address(address), arguments);

                // Send the OSC messsage
                _udpClient.SendMessageAsync(message).Wait();
            }
            catch (AggregateException ex) when (ex.InnerException is SocketException socketEx && socketEx.Message == "Connection refused")
            {
                _utilities.SendErrorMessage("Connection refused - No application is listening on " + _portNum + ".");
                Thread.Sleep(2000);
            }
            catch (Exception ex)
            {
                _utilities.SendErrorMessage(ex.Message);
            }
        }

        // Clean up resources when done
        public void Close()
        {
            _udpClient?.Close();
            _udpClient = null;
        }
    }
}