namespace hngkng.btgyro
{
	using System;

	class Program
	{
		static void Main(string[] args)
		{
			var cancelEvent = new System.Threading.ManualResetEvent(false);

			System.Console.CancelKeyPress += delegate(object sender, System.ConsoleCancelEventArgs args)
			{
				cancelEvent.Set();
			};

			var subject = new Device(Console.Write, Console.WriteLine);
			try
			{
				Console.WriteLine("Running...");
				Console.ReadKey();
				Console.WriteLine("Calibrating...");
				subject.Calibrate();
				cancelEvent.WaitOne();
			}
			finally
			{
				subject.Teardown();
				Console.WriteLine("Clean up complete...");
			}
		}
	}
}
