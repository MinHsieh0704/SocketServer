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
                PrintService.WriteLine("udp", ConsoleColor.Gray);
                SocketMode mode = SocketMode.Udp;

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

                                        string body = string.Join("", cards.Take(20).Reverse().ToArray());
                                        body = string.IsNullOrEmpty(body) ? "0" : body;

                                        PrintCard(remote, $"{Convert.ToInt64(head, 2)}{Convert.ToInt64(body, 2)}", "HID iCLASS Corporate 1000 35-bit (遠傳使用)");
                                    }
                                    else if (wiegandMode == 34)
                                    {
                                        string[] cards = Regex.Split(card, "").Where((n) => !string.IsNullOrEmpty(n)).Skip(card.Length == 35 ? 2 : 1).Reverse().Skip(1).ToArray();

                                        string head = string.Join("", cards.Skip(16).Reverse().ToArray());
                                        head = string.IsNullOrEmpty(head) ? "0" : head;

                                        string body = string.Join("", cards.Take(16).Reverse().ToArray());
                                        body = string.IsNullOrEmpty(body) ? "0" : body;

                                        PrintCard(remote, $"{Convert.ToInt64(head, 2)}{Convert.ToInt64(body, 2)}", "標準Wiegand 34-bit (中大型企業用)");
                                    }
                                    else if (wiegandMode == 26)
                                    {
                                        string[] cards = Regex.Split(card, "").Where((n) => !string.IsNullOrEmpty(n)).Skip(card.Length == 27 ? 2 : 1).Reverse().Skip(1).ToArray();

                                        string head = string.Join("", cards.Skip(16).Reverse().ToArray());
                                        head = string.IsNullOrEmpty(head) ? "0" : head;

                                        string body = string.Join("", cards.Take(16).Reverse().ToArray());
                                        body = string.IsNullOrEmpty(body) ? "0" : body;

                                        PrintCard(remote, $"{Convert.ToInt64(head, 2)}{Convert.ToInt64(body, 2)}", "標準Wiegand 26-bit (遠傳使用)");
                                    }
                                }
                                else if (bytesRec == 15)
                                {
                                    byte type = bytes[13];

                                    bytes = bytes.Skip(1).Take(10).ToArray();
                                    string card = Encoding.ASCII.GetString(bytes);

                                    if (type == 26)
                                        PrintCard(remote, $"{Convert.ToInt64(card)}", "不分區Wiegand 26-bit (中小企業用)");
                                    else if (type == 34)
                                        PrintCard(remote, $"{Convert.ToInt64(card)}", "不分區Wiegand 34-bit (中小企業用)");
                                }
                            }
                            catch (Exception ex)
                            {
                                ex = ExceptionHelper.GetReal(ex);
                                PrintService.Log($"{string.Join("", bytes.Select((n) => Convert.ToString(n, 16).PadLeft(2, '0')))}, {ex.Message}", Print.EMode.error);
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
