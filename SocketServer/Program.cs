using Min_Helpers;
using Min_Helpers.LogHelper;
using Min_Helpers.PrintHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketServer
{
    class Program
    {
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
    }
}
