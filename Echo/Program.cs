using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvLogTailing;
using Mono.Options;

namespace Echo
{
	/// <summary>
	/// This is a little test program that will echo tailed logs to console and optionally to 
	/// file also (means you can do a quick file diff to make sure you are picking up all logs)
	/// </summary>
	class Program
	{
		static void Main(string[] args)
		{
			string logFileOrDirPath = null;
			string directoryFilter = null;
			string excludeRegexString = null;
			string[] columns = null;
			bool echoToFile = false;

			var optionSet = new OptionSet
			{
				{ "p|path=", p => logFileOrDirPath = p },
				{ "f|filter=", f => directoryFilter = f },
				{ "x|excludeRegex=", x => excludeRegexString = x },
				{ "c|columns=", c => columns = c.Split(',').Select(x => x.Trim()).ToArray() },
				{ "ef|echotofile", p => echoToFile = true }
			};

			optionSet.Parse(args);

			Console.WriteLine("Press return to finish");

			var tailer = new CsvLogTailer();

			var exceptions = new List<Exception>();
			var exSub = tailer.Exceptions.Subscribe(ex =>
				{
					Console.WriteLine("PARSE EXCEPTION");
					exceptions.Add(ex);
				});

			tailer
				.Tail(new CsvLogTailerSettings
					{
						FileOrDirectoryPath = logFileOrDirPath,
						DirectoryFilter = directoryFilter,
						FileNameExcludeRegex = String.IsNullOrWhiteSpace(excludeRegexString) ? null : new Regex(excludeRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled),
						ColumnNamesProvider = file => columns
					})
				.Subscribe(log =>
				{
					var fileName = Path.GetFileName(log.FilePath);
					string logLine = String.Join("|", log.LogFields);

					if (echoToFile)
					{
						var dir = Path.GetDirectoryName(log.FilePath);
						var echoFilePath = Path.Combine(dir, fileName + ".echo");
						File.AppendAllText(echoFilePath, logLine + Environment.NewLine);
					}

					Console.WriteLine("[{0} {1}]: {2}", log.LogDateTime, fileName, logLine);
				});

			Console.ReadLine();

			exSub.Dispose();
			if (exceptions.Any())
			{
				foreach (Exception exception in exceptions)
					Console.WriteLine(exception);

				Console.WriteLine("Showing {0} exceptions", exceptions.Count);
			}
		}
	}
}
