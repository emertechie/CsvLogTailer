using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Options;

namespace Tailer
{
	class Program
	{
		static void Main(string[] args)
		{
			const char delimeter = '|';

			string fileOrDirPath = null;
			string outputPath = null;
			string directoryFilter = null;
			bool verbose = false;
			string[] columns = null;

			var optionSet = new OptionSet {
				{ "p|path=", p => fileOrDirPath = p },
				{ "f|filter=", f => directoryFilter = f },
				{ "o|output=", o => outputPath = o },
				{ "c|columns=", c => columns = c.Split(',').Select(x => x.Trim()).ToArray() },
				{ "v|verbose", v => verbose = true },
			};

			optionSet.Parse(args);

			bool writeToOutputPath = !String.IsNullOrWhiteSpace(outputPath);
			if (writeToOutputPath && !Directory.Exists(outputPath))
				Directory.CreateDirectory(outputPath);

			var logFileShadowWritersByPath = new Dictionary<string, StreamWriter>();
			var logMessagesByFile = new Dictionary<string, List<string>>();

			int messageColIndex = -1;
			if (columns != null)
				messageColIndex = columns.Select((c, idx) => new { match = c.Equals("message", StringComparison.OrdinalIgnoreCase), index = idx }).First(x => x.match).index;

			var tailer = new CsvTailer();
			tailer
				.Tail(fileOrDirPath, directoryFilter, columns)
				.Subscribe(log =>
					{
						if (columns != null)
						{
							if (!logMessagesByFile.ContainsKey(log.FilePath))
								logMessagesByFile.Add(log.FilePath, new List<string>());
							logMessagesByFile[log.FilePath].Add(log.LogFields[messageColIndex]);
						}

						string logLine = String.Join(delimeter.ToString(), log.LogFields);
						string fileName = Path.GetFileName(log.FilePath);

						if (verbose)
						{
							Console.Write(String.Format("[{0,-20}] ", fileName));
							Console.WriteLine(logLine);
						}

						if (writeToOutputPath)
						{
							StreamWriter writer;
							if (logFileShadowWritersByPath.ContainsKey(log.FilePath))
							{
								writer = logFileShadowWritersByPath[log.FilePath];
							}
							else
							{
								string logFileShadowPath = Path.Combine(outputPath, fileName + ".shadow");
								writer = File.Exists(logFileShadowPath)
								         	? new StreamWriter(File.OpenWrite(logFileShadowPath))
								         	: File.CreateText(logFileShadowPath);
							}

							writer.Write(logLine);
						}
					});

			Console.ReadLine();

			if (columns != null)
			{
				Console.WriteLine("Verifying that all log messages received are contiguous...");

				foreach (KeyValuePair<string, List<string>> pair in logMessagesByFile)
				{
					bool result = true;

					int prev = -1;
					var logMessageNumbers = pair.Value.Select(x => x.Substring("Test message ".Length)).Select(Int32.Parse);
					foreach (int messageNumber in logMessageNumbers)
					{
						if (messageNumber - prev != 1)
						{
							Console.WriteLine("ERROR: {0} is not next in sequence after {1}", messageNumber, prev);
							result = false;
							break;
						}
						prev = messageNumber;
					}

					Console.WriteLine("File:{0,40}. Result:{1}", pair.Key, result);
				}

				Console.WriteLine("...Done");
			}
		}
	}
}
