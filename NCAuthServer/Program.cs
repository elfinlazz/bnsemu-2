using NCAuthServer.Config;
using NCAuthServer.Network.Sts;
using NCommons.Utilities;
using System;
using System.Diagnostics;
using System.Threading;

namespace NCAuthServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "NCAuthServer";
            Console.WriteLine(@"    ____  _   _______ ______               ");
            Console.WriteLine(@"   / __ )/ | / / ___// ____/___ ___  __  __");
            Console.WriteLine(@"  / __  /  |/ /\__ \/ __/ / __ `__ \/ / / /");
            Console.WriteLine(@" / /_/ / /|  /___/ / /___/ / / / / / /_/ / ");
            Console.WriteLine(@"/_____/_/ |_//____/_____/_/ /_/ /_/\__,_/  ");
            Console.WriteLine();

            Stopwatch sw = Stopwatch.StartNew();
            Configuration.GetInstance();

            StsServer stsServer = new StsServer(Configuration.Network.PublicPort);
            stsServer.Start();
            Log.Info("StsServer started.");

            sw.Stop();
            Thread.Sleep(100);
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine("           Server start in {0}", (sw.ElapsedMilliseconds / 1000.0).ToString("0.00s"));
            Console.WriteLine("-------------------------------------------");

            /*while (true)
                Console.ReadLine();*/

            Process.GetCurrentProcess().WaitForExit();
        }
    }
}
