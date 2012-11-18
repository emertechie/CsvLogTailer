using System;

namespace CsvTailer
{
	public static class CsvTailerExtensions
	{
		public static IObservable<LogRecord> Tail(this CsvTailer tailer, string fileOrDirectoryPath)
		{
			return tailer.Tail(fileOrDirectoryPath, filePath => null);
		}

		public static IObservable<LogRecord> Tail(this CsvTailer tailer, string fileOrDirectoryPath, Func<string, string[]> columnsProvider)
		{
			var settings = new CsvTailerSettings(fileOrDirectoryPath)
				{
					ColumnsProvider = columnsProvider
				};
			return tailer.Tail(settings);
		}

		public static IObservable<LogRecord> Tail(this CsvTailer tailer, string directoryPath, string directoryFilter)
		{
			return tailer.Tail(directoryPath, directoryFilter, filePath => null);
		}

		public static IObservable<LogRecord> Tail(this CsvTailer tailer, string directoryPath, string directoryFilter, string[] columns)
		{
			return tailer.Tail(directoryPath, directoryFilter, _ => columns);
		}

		public static IObservable<LogRecord> Tail(this CsvTailer tailer, string directoryPath, string directoryFilter, Func<string, string[]> columnsProvider)
		{
			var settings = new CsvTailerSettings(directoryPath)
			{
				DirectoryFilter = directoryFilter
			};
			return tailer.Tail(settings);
		}
	}
}