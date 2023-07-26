using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Network_Scanner
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Scanning network...");
                List<Host> onlineHosts = ScanNetwork();
                Console.WriteLine("Scan complete.");

                Console.WriteLine("\nOnline Hosts:");
                foreach (var host in onlineHosts)
                {
                    Console.WriteLine($"Host: {host.IPAddress}, MAC Address: {host.MacAddress}, Vendor: {host.Vendor}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static List<Host> ScanNetwork()
        {
            List<Host> onlineHosts = new List<Host>();
            string baseIP = "192.168.1.";
            int timeout = 3000; // in milliseconds

            for (int i = 1; i <= 255; i++)
            {
                string ip = baseIP + i.ToString();

                try
                {
                    Ping ping = new Ping();
                    PingReply reply = ping.Send(ip, timeout);

                    if (reply.Status == IPStatus.Success)
                    {
                        Host host = new Host(ip);
                        onlineHosts.Add(host);
                        Console.WriteLine($"Host {ip} is online.");

                        // Check open ports on the online host
                        ScanPorts(host);
                    }
                }
                catch (Exception)
                {
                    // Host is offline or unreachable
                }
            }

            return onlineHosts;
        }

        static void ScanPorts(Host host)
        {
            // Define the ports you want to scan
            int[] portsToScan = { 21, 22, 80, 443, 3389, 8080 };

            foreach (int port in portsToScan)
            {
                try
                {
                    using (TcpClient tcpClient = new TcpClient())
                    {
                        tcpClient.Connect(host.IPAddress, port);
                        Console.WriteLine($"Port {port} is open on {host.IPAddress}.");

                        // Add the open port to the host's list of open ports
                        host.OpenPorts.Add(port);
                    }
                }
                catch (Exception)
                {
                    // Port is closed or unreachable
                }
            }
        }
    }

    class Host
    {
        public string IPAddress { get; }
        public string MacAddress { get; }
        public string Vendor { get; }
        public List<int> OpenPorts { get; }

        public Host(string ipAddress)
        {
            IPAddress = ipAddress;
            OpenPorts = new List<int>();
            MacAddress = GetMacAddress(ipAddress);
            Vendor = GetVendorFromMac(MacAddress);
        }

        private string GetMacAddress(string ipAddress)
        {
            try
            {
                PhysicalAddress macAddr = null;
                System.Net.IPAddress targetIp = System.Net.IPAddress.Parse(ipAddress);

                // Get the MAC address using ARP
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up && nic.GetIPProperties().GatewayAddresses.Count > 0)
                    {
                        foreach (UnicastIPAddressInformation ip in nic.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork && ip.Address.Equals(targetIp))
                            {
                                macAddr = nic.GetPhysicalAddress();
                                break;
                            }
                        }
                    }
                }

                if (macAddr == null)
                {
                    throw new Exception("Failed to retrieve MAC address.");
                }

                string macAddress = string.Join(":", Array.ConvertAll(macAddr.GetAddressBytes(), b => b.ToString("X2")));
                return macAddress;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while getting MAC address: {ex.Message}");
                return "00-00-00-00-00-00";
            }
        }

        private string GetVendorFromMac(string macAddress)
        {
            try
            {
                // Query the MAC address lookup service to get the vendor information
                using (UdpClient udpClient = new UdpClient())
                {
                    udpClient.Connect("api.macvendors.com", 80);
                    byte[] macBytes = macAddress.Replace("-", "").HexToByteArray();
                    udpClient.Send(macBytes, macBytes.Length);
                    IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Any, 0);
                    byte[] responseBytes = udpClient.Receive(ref endPoint);
                    string vendorInfo = System.Text.Encoding.ASCII.GetString(responseBytes);
                    return vendorInfo.Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while getting vendor information: {ex.Message}");
                return "Unknown Vendor";
            }
        }
    }

    public static class StringExtensions
    {
        public static byte[] HexToByteArray(this string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("The binary key cannot have an odd number of digits.");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + GetHexVal(hex[(i << 1) + 1]));
            }

            return arr;
        }

        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            //return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }
}
