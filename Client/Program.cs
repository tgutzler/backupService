using System;
using System.Threading;

namespace Client
{
    class Program
    {
        static CancellationTokenSource _cts = new CancellationTokenSource();

        static void Main(string[] args)
        {
            using (_cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += Console_CancelKeyPress;
                var app = new App("http://localhost:52671/", _cts.Token);

                try
                {
                    app.RunAsync().Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _cts.Cancel();
        }
    }
}