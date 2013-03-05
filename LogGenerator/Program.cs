using System;
using System.Threading;
using NLog;

namespace LogGenerator
{
	class Program
	{
		public static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.WriteLine("No args!");
				return;
			}

			Console.WriteLine("Press return to start");
			Console.ReadLine();

			int count = Int32.Parse(args[0]);
			Console.WriteLine("Writing {0} logs", count);

			for (int i = 1; i <= count; i++)
			{
				Logger.Debug("Log " + i + "asdf askjfdh aslfdkh asfdjklh asdkfh asfdjkh asldkfh aslkdfh askdjlfh askd alskdjfh asdf asldkfh aslkdfh askdjlfh askd alskdjfh asdf asldkfh aslkdfh askdjlfh askd alskdjfh asdf");
				Thread.Sleep(5);

				if (i % 100 == 0)
				{
					Console.WriteLine("Wrote log " + i);
				}
			}
		}
	}
}
