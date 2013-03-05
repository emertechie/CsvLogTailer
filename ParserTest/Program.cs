using System;
using System.Linq;
using System.Text;

namespace ParserTest
{
	class Program
	{
		static void Main(string[] args)
		{
			var parser = new CsvParser.CsvParser('|');

			var mem1 = GC.GetTotalMemory(true);
			long mem2;

			{
				var logs = parser.ParseFile(@"C:\Temp\DeleteMe\Logs\LogGenerator.log", Encoding.UTF8).ToArray();

				logs = null;

				mem2 = GC.GetTotalMemory(true);

				logs = null;
			}

			GC.Collect();

			var mem3 = GC.GetTotalMemory(true);

			Console.WriteLine("Mem before: {0} KB, after: {1} KB. Diff: {2} KB", (int)(mem1 / 1024), (int)(mem2 / 1024), (int)((mem2 - mem1) / 1024));
			Console.WriteLine("After GC.Collect: {0} KB", (int)(mem3 / 1024));
		}
	}
}
