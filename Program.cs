namespace hjo.btgyro
{
	using System;

	class Program
	{
		static void Main(string[] args)
		{
			var subject = new Device(Console.Write, Console.WriteLine);
			try
			{
				Console.WriteLine("Running...");

				Console.WriteLine("R = calibrate/reset");
				Console.WriteLine("X = quit");

				ConsoleKeyInfo keyInfo = Console.ReadKey();
				while (keyInfo.Key != ConsoleKey.X)
				{
					if (keyInfo.Key == ConsoleKey.R)
					{
						subject.Calibrate();
					}

					Console.WriteLine("R = calibrate/reset");
					Console.WriteLine("X = quit");
					keyInfo = Console.ReadKey();
				}
			}
			finally
			{
				subject.Teardown();
				Console.WriteLine("Clean up complete...");
			}
		}
	}
}
