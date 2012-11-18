using System;

namespace CsvTailer
{
	public class LogRecord
	{
		private readonly string filePath;
		private readonly long streamPosition;
		private readonly DateTime logDateTime;
		private readonly string[] logFields;
		private readonly string[] columnNames;

		public LogRecord(string filePath, long streamPosition, DateTime logDateTime, string[] logFields, string[] possiblyNullColumnNames)
		{
			this.filePath = filePath;
			this.streamPosition = streamPosition;
			this.logDateTime = logDateTime;
			this.logFields = logFields;
			this.columnNames = possiblyNullColumnNames;
		}

		public string FilePath
		{
			get { return filePath; }
		}

		public long StreamPosition
		{
			get { return streamPosition; }
		}

		public DateTime LogDateTime
		{
			get { return logDateTime; }
		}

		public string[] LogFields
		{
			get { return logFields; }
		}

		public string[] ColumnNames
		{
			get { return columnNames; }
		}
	}
}