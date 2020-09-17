using Min_Helpers;
using Min_Helpers.LogHelper;
using Min_Helpers.PrintHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketServer
{
    class Program
    {
        enum SocketMode
        {
            Tcp,
            Udp
        }

        static Print PrintService { get; set; } = null;
        static Log LogService { get; set; } = null;

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");

            try
            {
                LogService = new Log();
                PrintService = new Print();

                LogService.Write("");
                PrintService.Log("App Start", Print.EMode.info);

                IPAddress ip = IPAddress.Any;

                PrintService.Write("Mode (tcp / udp): ", Print.EMode.question);
                PrintService.WriteLine("tcp", ConsoleColor.Gray);
                SocketMode mode = SocketMode.Tcp;

                PrintService.Write("Listen Port: ", Print.EMode.question);
                int port = Convert.ToInt32(Console.ReadLine());

                StartClient(ip, port, mode);
            }
            catch (Exception ex)
            {
                ex = ExceptionHelper.GetReal(ex);
                PrintService.Log($"App Error, {ex.Message}", Print.EMode.error);
            }
            finally
            {
                PrintService.Log("App End", Print.EMode.info);
                Console.ReadKey();

                Environment.Exit(0);
            }
        }

        private static void StartClient(IPAddress ip, int port, SocketMode mode)
        {
            try
            {
                IPEndPoint iPEnd = new IPEndPoint(ip, port);
                SocketType socketType = mode == SocketMode.Tcp ? SocketType.Stream : SocketType.Dgram;
                ProtocolType protocolType = mode == SocketMode.Tcp ? ProtocolType.Tcp : ProtocolType.Udp;

                using (Socket server = new Socket(AddressFamily.InterNetwork, socketType, protocolType))
                {
                    server.Bind(iPEnd);

                    if (mode == SocketMode.Tcp)
                    {
                        server.Listen(100);

                        List<Socket> clients = new List<Socket>();

                        while (true)
                        {
                            Socket client = server.Accept();
                            EndPoint remote = client.RemoteEndPoint;

                            PrintService.Log($"Client<{remote}> is connecting", Print.EMode.success);

                            clients.Add(client);

                            Task.Run(() =>
                            {
                                try
                                {
                                    while (IsSocketConnected(client))
                                    {

                                        byte[] bytes = new byte[1024];

                                        int bytesRec = client.Receive(bytes);

                                        string message = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                                        message = message.Replace("\n", "").Replace("\r", "");

                                        if (!string.IsNullOrEmpty(message))
                                        {
                                            PrintService.Log($"Client<{remote}>: {message}", Print.EMode.message);

                                            byte[] byteData = Encoding.ASCII.GetBytes($"OK\r\n");
                                            client.Send(byteData);
                                        }
                                    }

                                    client.Shutdown(SocketShutdown.Both);
                                    client.Close();
                                }
                                catch (Exception)
                                {
                                }
                                finally
                                {
                                    clients = clients.Where((n) => n.Handle != client.Handle).ToList();
                                    PrintService.Log($"Client<{remote}> was disconnected", Print.EMode.warning);
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static bool IsSocketConnected(Socket client)
        {
            try
            {
                return !(client.Poll(1, SelectMode.SelectRead) && client.Available == 0);
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}
