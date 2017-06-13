using System;
using System.Threading;

namespace Client
{
    class Program
    {
        static CancellationTokenSource _cts = new CancellationTokenSource();
        static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            var app = new App("http://localhost:54426/", _cts.Token);

            app.RunAsync().Wait();

            _cts.Dispose();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _cts.Cancel();
        }
    }
}