using System;
using CsvLogTailer.Bookmarks;

namespace CsvLogTailer
{
	public static class CsvLogTailerExtensions
	{
		public static IObservable<LogRecord> Tail(this CsvLogTailer tailer, string filePath, ILogFileBookmarkRepository repository = null)
		{
			return tailer.Tail(filePath, null, file => null, repository);
		}

		public static IObservable<LogRecord> Tail(this CsvLogTailer tailer, string filePath, string[] columnNames, ILogFileBookmarkRepository repository = null)
		{
			return tailer.Tail(filePath, null, file => columnNames, repository);
		}

		public static IObservable<LogRecord> Tail(this CsvLogTailer tailer, string directoryPath, string directoryFilter, ILogFileBookmarkRepository repository = null)
		{
			return tailer.Tail(directoryPath, directoryFilter, filePath => null, repository);
		}

		public static IObservable<LogRecord> Tail(this CsvLogTailer tailer, string directoryPath, string directoryFilter, string[] columnNames, ILogFileBookmarkRepository repository = null)
		{
			return tailer.Tail(directoryPath, directoryFilter, filePath => columnNames, repository);
		}

		private static IObservable<LogRecord> Tail(
			this CsvLogTailer tailer,
			string directoryPath,
			string directoryFilter,
			Func<string, string[]> columnNamesProvider,
			ILogFileBookmarkRepository repository = null)
		{
			var settings = new CsvLogTailerSettings
			{
				FileOrDirectoryPath = directoryPath,
				DirectoryFilter = directoryFilter,
				ColumnNamesProvider = columnNamesProvider
			};

			return repository == null
				? tailer.Tail(settings)
				: tailer.Tail(settings, repository);
		}
	}
}