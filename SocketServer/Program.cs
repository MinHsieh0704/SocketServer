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
using System.Text.RegularExpressions;
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
                SocketMode mode = Console.ReadLine() == "tcp" ? SocketMode.Tcp : SocketMode.Udp;

                PrintService.Write("Listen Port: ", Print.EMode.question);
                int port = Convert.ToInt32(Console.ReadLine());

                PrintService.WriteLine("", Print.EMode.message);

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

                                        try
                                        {
                                            int bytesRec = client.Receive(bytes);
                                            if (bytesRec == 0) continue;

                                            if (bytes[0] == 49)
                                            {
                                                byte wiegandMode = bytes[2];

                                                bytes = bytes.Skip(3).Take(bytesRec - 3).ToArray();

                                                if (wiegandMode == 24)
                                                {
                                                    string card = string.Join("", bytes.Select((n) => Convert.ToString(n, 2).PadLeft(8, '0')));

                                                    string head = string.Join("", card.Substring(0, 8));
                                                    head = string.IsNullOrEmpty(head) ? "0" : head;
                                                    head = Convert.ToInt64(head, 2).ToString().PadLeft(5, '0');

                                                    string body = string.Join("", card.Substring(8, 16));
                                                    body = string.IsNullOrEmpty(body) ? "0" : body;
                                                    body = Convert.ToInt64(body, 2).ToString().PadLeft(5, '0');

                                                    card = Convert.ToInt64(card, 2).ToString().PadLeft(10, '0');

                                                    PrintCard(remote, $"{head}:{body}", "iClass Wiegand 26-bit", $" {card}", "Mifare Wiegand 26-bit");
                                                }
                                                else if (wiegandMode == 32)
                                                {
                                                    string card = string.Join("", bytes.Select((n) => Convert.ToString(n, 2).PadLeft(8, '0')));

                                                    string head = string.Join("", card.Substring(0, 16));
                                                    head = string.IsNullOrEmpty(head) ? "0" : head;
                                                    head = Convert.ToInt64(head, 2).ToString().PadLeft(5, '0');

                                                    string body = string.Join("", card.Substring(16, 16));
                                                    body = string.IsNullOrEmpty(body) ? "0" : body;
                                                    body = Convert.ToInt64(body, 2).ToString().PadLeft(5, '0');

                                                    card = Convert.ToInt64(card, 2).ToString().PadLeft(10, '0');

                                                    PrintCard(remote, $"{head}:{body}", "iClass Wiegand 34-bit", $" {card}", "Mifare Wiegand 34-bit");
                                                }
                                            }

                                            client.Send(new byte[] { 0x40 });
                                        }
                                        catch (Exception ex)
                                        {
                                            ex = ExceptionHelper.GetReal(ex);
                                            PrintService.Log($"{string.Join("", bytes.Select((n) => Convert.ToString(n, 16).PadLeft(2, '0'))).ToUpper()}, {ex.Message}", Print.EMode.error);

                                            client.Send(new byte[] { 0x40 });
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
                    else
                    {
                        while (true)
                        {
                            EndPoint remote = new IPEndPoint(ip, port);

                            byte[] bytes = new byte[1024];

                            try
                            {
                                int bytesRec = server.ReceiveFrom(bytes, ref remote);
                                if (bytesRec == 0) continue;

                                if (bytesRec == 14)
                                {
                                    byte wiegandMode = bytes[10];

                                    bytes = bytes.Skip(1).Take(9).ToArray();

                                    string card = string.Join("", bytes.Select((n) => Convert.ToString(n, 2).PadLeft(8, '0')));
                                    card = card.Substring(0, card.LastIndexOf("10101"));

                                    if (wiegandMode == 35)
                                    {
                                        string[] cards = Regex.Split(card, "").Where((n) => !string.IsNullOrEmpty(n)).Skip(card.Length == 36 ? 3 : 2).Reverse().Skip(1).ToArray();

                                        string head = string.Join("", cards.Skip(20).Reverse().ToArray());
                                        head = string.IsNullOrEmpty(head) ? "0" : head;
                                        head = Convert.ToInt64(head, 2).ToString().PadLeft(5, '0');

                                        string body = string.Join("", cards.Take(20).Reverse().ToArray());
                                        body = string.IsNullOrEmpty(body) ? "0" : body;
                                        body = Convert.ToInt64(body, 2).ToString().PadLeft(5, '0');

                                        PrintCard(remote, $"{head}:{body}", "HID iCLASS Corporate 1000 35-bit (遠傳使用)");
                                    }
                                    else if (wiegandMode == 34)
                                    {
                                        string[] cards = Regex.Split(card, "").Where((n) => !string.IsNullOrEmpty(n)).Skip(card.Length == 35 ? 2 : 1).Reverse().Skip(1).ToArray();

                                        string head = string.Join("", cards.Skip(16).Reverse().ToArray());
                                        head = string.IsNullOrEmpty(head) ? "0" : head;
                                        head = Convert.ToInt64(head, 2).ToString().PadLeft(5, '0');

                                        string body = string.Join("", cards.Take(16).Reverse().ToArray());
                                        body = string.IsNullOrEmpty(body) ? "0" : body;
                                        body = Convert.ToInt64(body, 2).ToString().PadLeft(5, '0');

                                        PrintCard(remote, $"{head}:{body}", "標準Wiegand 34-bit (中大型企業用)");
                                    }
                                    else if (wiegandMode == 26)
                                    {
                                        string[] cards = Regex.Split(card, "").Where((n) => !string.IsNullOrEmpty(n)).Skip(card.Length == 27 ? 2 : 1).Reverse().Skip(1).ToArray();

                                        string head = string.Join("", cards.Skip(16).Reverse().ToArray());
                                        head = string.IsNullOrEmpty(head) ? "0" : head;
                                        head = Convert.ToInt64(head, 2).ToString().PadLeft(5, '0');

                                        string body = string.Join("", cards.Take(16).Reverse().ToArray());
                                        body = string.IsNullOrEmpty(body) ? "0" : body;
                                        body = Convert.ToInt64(body, 2).ToString().PadLeft(5, '0');

                                        PrintCard(remote, $"{head}:{body}", "標準Wiegand 26-bit (遠傳使用)");
                                    }
                                }
                                else if (bytesRec == 15)
                                {
                                    byte type = bytes[13];
                                    bytes = bytes.Skip(1).Take(10).ToArray();

                                    string card = Encoding.ASCII.GetString(bytes);
                                    card = card.PadLeft(10, '0');

                                    if (type == 26)
                                    {
                                        PrintCard(remote, $" {card}", "不分區Wiegand 26-bit (中小企業用)");
                                    }
                                    else if (type == 34)
                                    {
                                        PrintCard(remote, $" {card}", "不分區Wiegand 34-bit (中小企業用)");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ex = ExceptionHelper.GetReal(ex);
                                PrintService.Log($"{string.Join("", bytes.Select((n) => Convert.ToString(n, 16).PadLeft(2, '0'))).ToUpper()}, {ex.Message}", Print.EMode.error);
                            }
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

        private static void PrintCard(EndPoint remote, string card1, string type1, string card2, string type2)
        {
            try
            {
                PrintService.Write($"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")} ---> ", Print.EMode.message);
                PrintService.Write($"Client<{remote}>: ", Print.EMode.message);
                PrintService.Write($"{card1} ", Print.EMode.info);
                PrintService.Write($"({type1})", Print.EMode.message);
                PrintService.Write($"\n                                {new string(' ', remote.ToString().Length)}   ", Print.EMode.message);
                PrintService.Write($"{card2} ", Print.EMode.info);
                PrintService.WriteLine($"({type2})", Print.EMode.message);

                LogService.Write($"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}  Message ---> [{Thread.CurrentThread.ManagedThreadId}] Client<{remote}>: {card1} ({type1}), {card2} ({type2})");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static void PrintCard(EndPoint remote, string card, string type)
        {
            try
            {
                PrintService.Write($"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")} ---> ", Print.EMode.message);
                PrintService.Write($"Client<{remote}>: ", Print.EMode.message);
                PrintService.Write($"{$"{card}",10} ", Print.EMode.info);
                PrintService.WriteLine($"({type})", Print.EMode.message);

                LogService.Write($"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}  Message ---> [{Thread.CurrentThread.ManagedThreadId}] Client<{remote}>: {card} ({type})");
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
