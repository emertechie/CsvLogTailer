using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvLogTailer;
using CsvLogTailer.Bookmarks;
using Xunit;
using Xunit.Extensions;

namespace CsvTailer.Tests
{
	public class CsvTailerTests
	{
		internal const char DefaultDelimeter = '|';

		public abstract class FileTailerTestsBase : IDisposable
		{
			protected static readonly TimeSpan TimeoutTimeSpan = TimeSpan.FromSeconds(Debugger.IsAttached ? 60 : 5);
			protected static readonly string[] LogColumns = new[] { "DateTime", "Namespace", "Machine", "Level", "Message", "Exception" };

			protected FileTailerTestsBase()
			{
				ObservedEvents = new BlockingCollection<Notification<LogRecord>>();
			}

			~FileTailerTestsBase()
			{
				Dispose(false);
			}

			protected string LogFilePath { get; set; }

			protected IDisposable TailerSubscription { get; set; }

			protected BlockingCollection<Notification<LogRecord>> ObservedEvents { get; private set; }

			protected void Dispose(bool disposing)
			{
				if (disposing)
					DisposeManagedObjects();
			}

			protected virtual void DisposeManagedObjects()
			{
				if (TailerSubscription != null)
					TailerSubscription.Dispose();

				if (LogFilePath != null && File.Exists(LogFilePath))
					File.Delete(LogFilePath);
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected void Write(string text, string filePath = null)
			{
				filePath = filePath ?? LogFilePath;

				using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
				{
					using (var writer = new StreamWriter(stream, Encoding.UTF8))
					{
						writer.WriteLine(text);
						writer.Flush();
					}
				}
			}

			protected LogRecord GetNext(BlockingCollection<Notification<LogRecord>> observedEvents)
			{
				Notification<LogRecord> observed = GetNextNotification(observedEvents);

				if (observed.Kind == NotificationKind.OnError)
					Assert.True(observed.Kind != NotificationKind.OnError, observed.Exception.Message);

				Assert.Equal(NotificationKind.OnNext, observed.Kind);
				return observed.Value;
			}

			protected static string CreateLogLine(
				string dateTimeString = "2012-06-01 18:34:49.8539",
				string nameSpace = "Some.Namespace",
				string machineName = "MACHINE-X",
				string logLevel = "INFO",
				string logMessage = "Message 1",
				string exception = null,
				char delimeter = DefaultDelimeter)
			{
				return String.Join(delimeter.ToString(), dateTimeString, nameSpace, machineName, logLevel, logMessage, exception);
			}

			protected static void AssertLogLineExpected(string filePath, string expectedLogLine, LogRecord actualValue, char delimeter = DefaultDelimeter)
			{
				Assert.Equal(filePath, actualValue.FilePath);
				AssertLogLineExpected(expectedLogLine, actualValue, delimeter);
			}

			protected static void AssertLogLineExpected(string expectedLogLine, LogRecord actualValue, char delimeter = DefaultDelimeter)
			{
				string[] expected = expectedLogLine
					.Split(delimeter)
					.Select(x => x.TrimStart('"').TrimEnd('"').Replace("\r\n","\n"))
					.ToArray();

				Assert.Equal(expected, actualValue.LogFields);
			}

			private Notification<LogRecord> GetNextNotification(BlockingCollection<Notification<LogRecord>> observedEvents)
			{
				Notification<LogRecord> observed;
				Assert.True(observedEvents.TryTake(out observed, TimeoutTimeSpan), "Timed out waiting for line");
				return observed;
			}
		}

		public class Given_empty_file : FileTailerTestsBase
		{
			private readonly CsvLogTailer.CsvLogTailer sut;
			private bool ignoreExceptions;

			public Given_empty_file()
			{
				LogFilePath = Path.GetTempFileName();

				sut = new CsvLogTailer.CsvLogTailer();
				sut.Exceptions.Subscribe(ex =>
				{
					if (!ignoreExceptions)
						ObservedEvents.CompleteAdding();
					Console.WriteLine(ex);
				});
			}

			[Fact]
			public void CanObserveLogWithNoException()
			{
				TailerSubscription = sut.Tail(LogFilePath).MaintainObservedEventsCollection(ObservedEvents);

				string logLine = CreateLogLine(exception: null);

				Write(logLine);
				LogRecord next = GetNext(ObservedEvents);

				AssertLogLineExpected(logLine, next);
			}

			[Fact]
			public void CanObserveLogWithSingleLineException()
			{
				TailerSubscription = sut.Tail(LogFilePath).MaintainObservedEventsCollection(ObservedEvents);

				string logLine = CreateLogLine(exception: "foo");

				Write(logLine);
				LogRecord next = GetNext(ObservedEvents);

				AssertLogLineExpected(logLine, next);
			}

			[Fact]
			public void CanObserveLogWithColumns()
			{
				var expectedColumns = new[] { "A", "B" };
				TailerSubscription = sut.Tail(LogFilePath, expectedColumns).MaintainObservedEventsCollection(ObservedEvents);

				Write(CreateLogLine());
				LogRecord next = GetNext(ObservedEvents);

				Assert.Equal(expectedColumns, next.ColumnNames);
			}

			[Theory]
			[InlineData("\r\n")]
			[InlineData("\n")]
			public void CanObserveLogWithMultiLineException(string newline)
			{
				TailerSubscription = sut.Tail(LogFilePath).MaintainObservedEventsCollection(ObservedEvents);

				string quote = "\"";
				string logLine = CreateLogLine(exception: quote + "foo" + newline + "bar" + quote);

				Write(logLine);
				LogRecord next = GetNext(ObservedEvents);

				AssertLogLineExpected(logLine, next);
			}

			[Fact]
			public void WillMakeBestEffortToRecoverFromIncompleteQuotedValues_1()
			{
				TailerSubscription = sut.Tail(LogFilePath).MaintainObservedEventsCollection(ObservedEvents);

				string logLine1 = CreateLogLine(logMessage: "\"Bad message 1 without trailing quote");
				string logLine2 = CreateLogLine(logMessage: "Good message 1");

				ignoreExceptions = true;

				Write(logLine1);
				Write(logLine2);

				LogRecord goodLog1 = GetNext(ObservedEvents);
				AssertLogLineExpected(logLine2, goodLog1);
			}

			[Fact]
			public void WillMakeBestEffortToRecoverFromIncompleteQuotedValues_3()
			{
				TailerSubscription = sut.Tail(LogFilePath).MaintainObservedEventsCollection(ObservedEvents);

				string logLine1 = CreateLogLine(logMessage: "\"Bad message 1 without trailing quote");
				string logLine2 = CreateLogLine(logMessage: "Good message 1");
				string logLine3 = CreateLogLine(logMessage: "\"Another bad message \n with new lines \r\n also");
				string logLine4 = CreateLogLine(logMessage: "\"One more bad one to be sure");
				string logLine5 = CreateLogLine(logMessage: "Good message 2");

				ignoreExceptions = true;

				Write(logLine1);
				Write(logLine2);
				Write(logLine3);
				Write(logLine4);
				Write(logLine5);

				// Note: The first (good) message is also thrown away as the parser will attempt to parse all 3 lines together.
				// On error, the recovery mechanism will move the current line down the file until it can parse all the content successfully.
				// Because normal use case will be for the parser to parse new lines at the end of the file as they appear, this means we 
				// should be able to skip poorly formatted lines without too much loss. Parsing a whole file for the first time though 
				// could result in many logs not being picked up

				//LogRecord goodLog1 = GetNext(ObservedEvents);
				//AssertLogLineExpected(logLine2, goodLog1);

				LogRecord goodLog2 = GetNext(ObservedEvents);
				AssertLogLineExpected(logLine5, goodLog2);
			}

			[Theory]
			[InlineData(null)]
			[InlineData("single line exception")]
			[InlineData("\"multi \n line \r\n exception\"")]
			public void CanObserveMultipleLogs(string exception)
			{
				TailerSubscription = sut.Tail(LogFilePath).MaintainObservedEventsCollection(ObservedEvents);

				string logLine1 = CreateLogLine(logMessage: "message 1", exception: exception);
				string logLine2 = CreateLogLine(logMessage: "message 2", exception: exception);

				Write(logLine1);
				LogRecord first = GetNext(ObservedEvents);
				AssertLogLineExpected(logLine1, first);

				Write(logLine2);
				LogRecord second = GetNext(ObservedEvents);
				AssertLogLineExpected(logLine2, second);
			}

			[Theory]
			[InlineData(null)]
			[InlineData("single line exception")]
			[InlineData("\"multi \n line \r\n exception\"")]
			public void CanObserveMultipleLogs_WrittenInQuickSuccession(string exception)
			{
				TailerSubscription = sut.Tail(LogFilePath).MaintainObservedEventsCollection(ObservedEvents);

				string logLine1 = CreateLogLine(logMessage: "message 1", exception: exception);
				string logLine2 = CreateLogLine(logMessage: "message 2", exception: exception);
				string logLine3 = CreateLogLine(logMessage: "message 3", exception: exception);

				Write(logLine1);
				Write(logLine2);
				Write(logLine3);

				AssertLogLineExpected(logLine1, GetNext(ObservedEvents));
				AssertLogLineExpected(logLine2, GetNext(ObservedEvents));
				AssertLogLineExpected(logLine3, GetNext(ObservedEvents));
			}

			[Fact]
			public void CanStoreLastPositionMetadataForEachLog()
			{
				var settings = new CsvLogTailerSettings { FileOrDirectoryPath = LogFilePath };
				var positionRepository = new FakeLogFileBookmarkRepository();

				TailerSubscription = sut.Tail(settings, positionRepository).MaintainObservedEventsCollection(ObservedEvents);

				string logLine1 = CreateLogLine(logMessage: "message 1");
				string logLine2 = CreateLogLine(logMessage: "message 2");

				Write(logLine1);
				LogRecord first = GetNext(ObservedEvents);
				LogFileBookmark metadataAfterFirstLog = positionRepository.Metadata.Single().Value;
				
				Write(logLine2);
				LogRecord second = GetNext(ObservedEvents);
				LogFileBookmark metadataAfterSecondLog = positionRepository.Metadata.Single().Value;

				Assert.Equal(DateTime.Parse(first.LogFields[0]), metadataAfterFirstLog.LogDateTime);
				Assert.Equal(DateTime.Parse(second.LogFields[0]), metadataAfterSecondLog.LogDateTime);
			}

			[Fact]
			public void CanStartTailingFileFromLastPosition()
			{
				var settings = new CsvLogTailerSettings
				{
					FileOrDirectoryPath = LogFilePath,
					BookmarkRepositoryUpdateFrequency = TimeSpan.FromSeconds(0.1)
				};

				var positionRepository = new FakeLogFileBookmarkRepository();

				TailerSubscription = sut.Tail(settings, positionRepository).MaintainObservedEventsCollection(ObservedEvents);

				var dateTime = new DateTime(2012, 11, 18, 12, 1, 0);
				var secondLogDateTimeString = dateTime.AddMinutes(1).ToString();
				string logLine1 = CreateLogLine(logMessage: "message 1", dateTimeString: dateTime.ToString());
				string logLine2 = CreateLogLine(logMessage: "message 2", dateTimeString: secondLogDateTimeString);
				string logLine3 = CreateLogLine(logMessage: "message 3", dateTimeString: dateTime.AddMinutes(2).ToString());
				string logLine4 = CreateLogLine(logMessage: "message 4", dateTimeString: dateTime.AddMinutes(3).ToString());

				Write(logLine1);
				Write(logLine2);

				LogFileBookmark metadataForSecondLog = positionRepository.BookmarksSeen
					.Do(x => Console.WriteLine("Saw {0}", x.LogDateTime))
					.Where(x => x.LogDateTime.ToString() == secondLogDateTimeString)
					.Timeout(TimeoutTimeSpan)
					.First();

				TailerSubscription.Dispose();

				Write(logLine3);
				Write(logLine4);

				// Now tail the file again, but start from position given by 'metadataAfterSecondLog' variable
				// We will start reading from the same DateTime as the second log so we should read logs 2, 3 + 4.

				// The reason we read log 2 again is because of the case where another log came in with the same timestamp 
				// as log 2 but was not processed before the tailer shut down. So, we favour the occasional duplicate log
				// over the possibly of missing some

				var positionRepository2 = new FakeLogFileBookmarkRepository();
				positionRepository2.AddOrUpdate(metadataForSecondLog);

				var observeredEvents2 = new BlockingCollection<Notification<LogRecord>>();
				using (sut.Tail(settings, positionRepository2).MaintainObservedEventsCollection(observeredEvents2))
				{
					LogRecord duplicate = GetNext(observeredEvents2);
					AssertLogLineExpected(logLine2, duplicate);

					LogRecord first = GetNext(observeredEvents2);
					AssertLogLineExpected(logLine3, first);

					LogRecord second = GetNext(observeredEvents2);
					AssertLogLineExpected(logLine4, second);
				}
			}

			private class FakeLogFileBookmarkRepository : ILogFileBookmarkRepository
			{
				public readonly Dictionary<string, LogFileBookmark> Metadata = new Dictionary<string, LogFileBookmark>();

				private readonly Subject<LogFileBookmark> bookmarksSubject = new Subject<LogFileBookmark>();

				public LogFileBookmark Get(string filePath)
				{
					return Metadata.ContainsKey(filePath) ? Metadata[filePath] : null;
				}

				public Subject<LogFileBookmark> BookmarksSeen
				{
					get { return bookmarksSubject; }
				}

				public void AddOrUpdate(LogFileBookmark bookmark)
				{
					if (Metadata.ContainsKey(bookmark.FilePath))
						Metadata[bookmark.FilePath] = bookmark;
					else
						Metadata.Add(bookmark.FilePath, bookmark);

					bookmarksSubject.OnNext(bookmark);
				}
			}
		}

		public class Given_no_file_exists : FileTailerTestsBase
		{
			private readonly CsvLogTailer.CsvLogTailer sut;

			public Given_no_file_exists()
			{
				sut = new CsvLogTailer.CsvLogTailer();
				sut.Exceptions.Subscribe(ex => Console.WriteLine(ex));
			}

			[Fact]
			public void WhenTailed_WillBlockUntilFileCreatedAndFirstLineWritten()
			{
				LogFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

				TailerSubscription = sut.Tail(LogFilePath).MaintainObservedEventsCollection(ObservedEvents);

				var waiting = new ManualResetEventSlim();
				Task<LogRecord> task = Task.Factory.StartNew(() =>
					{
						waiting.Set();
						return GetNext(ObservedEvents);
					});

				// Make sure Task has spun up
				Assert.True(waiting.Wait(TimeoutTimeSpan));
				// Make sure (as much as possible anyway), that the task continues and starts waiting in GetNext(ObservedEvents) call
				Thread.Sleep(200);

				using (File.Create(LogFilePath));
				string logLine = CreateLogLine();
				Write(logLine);

				task.Wait();
				LogRecord next = task.Result;
				AssertLogLineExpected(logLine, next);
			}
		}

		public class Given_empty_directory : FileTailerTestsBase
		{
			private readonly string logsDirectory;

			public Given_empty_directory()
			{
				logsDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
				Directory.CreateDirectory(logsDirectory);
			}

			protected override void DisposeManagedObjects()
			{
				base.DisposeManagedObjects();
				if (Directory.Exists(logsDirectory))
					Directory.Delete(logsDirectory, true);
			}

			[Fact]
			public void CanTailAllFilesInDirectory_WithNoFilter()
			{
				var sut = new CsvLogTailer.CsvLogTailer();
				TailerSubscription = sut.Tail(logsDirectory, LogColumns).MaintainObservedEventsCollection(ObservedEvents);

				string file1 = CreateLogFile(logsDirectory, "logfile1.txt");
				string file2 = CreateLogFile(logsDirectory, "logfile2.txt");

				string file1Line1 = CreateLogLine(logMessage: "File1-Line1");
				string file1Line2 = CreateLogLine(logMessage: "File1-Line2");
				string file2Line1 = CreateLogLine(logMessage: "File2-Line1");
				string file2Line2 = CreateLogLine(logMessage: "File2-Line2");

				Write(file1Line1, file1);
				Write(file2Line1, file2);
				Write(file1Line2, file1);
				Write(file2Line2, file2);

				var logRecords = new List<LogRecord>
				{
					GetNext(ObservedEvents),
					GetNext(ObservedEvents),
					GetNext(ObservedEvents),
					GetNext(ObservedEvents)
				};

				int messageColIndex = Array.IndexOf(LogColumns, "Message"); 
				AssertLogLineExpected(file1, file1Line1, logRecords.Single(x => x.LogFields[messageColIndex] == "File1-Line1"));
				AssertLogLineExpected(file1, file1Line2, logRecords.Single(x => x.LogFields[messageColIndex] == "File1-Line2"));
				AssertLogLineExpected(file2, file2Line1, logRecords.Single(x => x.LogFields[messageColIndex] == "File2-Line1"));
				AssertLogLineExpected(file2, file2Line2, logRecords.Single(x => x.LogFields[messageColIndex] == "File2-Line2"));
			}

			private static string CreateLogFile(string logsDirectory, string fileName)
			{
				var file1 = Path.Combine(logsDirectory, fileName);
				using (File.Create(file1))
					;
				return file1;
			}

			[Fact]
			public void CanTailAllFilesInDirectory_WithAFilter()
			{
				const string directoryFilter = "*2.txt";

				var sut = new CsvLogTailer.CsvLogTailer();
				TailerSubscription = sut.Tail(logsDirectory, directoryFilter, LogColumns).MaintainObservedEventsCollection(ObservedEvents);

				string file1 = CreateLogFile(logsDirectory, "logfile1.txt");
				string file2 = CreateLogFile(logsDirectory, "logfile2.txt");

				string file1Line1 = CreateLogLine(logMessage: "File1-Line1");
				string file1Line2 = CreateLogLine(logMessage: "File1-Line2");
				string file2Line1 = CreateLogLine(logMessage: "File2-Line1");
				string file2Line2 = CreateLogLine(logMessage: "File2-Line2");

				Write(file1Line1, file1);
				Write(file2Line1, file2);
				Write(file1Line2, file1);
				Write(file2Line2, file2);

				var logRecords = new List<LogRecord>
				{
					GetNext(ObservedEvents),
					GetNext(ObservedEvents)
				};

				int messageColIndex = Array.IndexOf(LogColumns, "Message");
				AssertLogLineExpected(file2, file2Line1, logRecords.Single(x => x.LogFields[messageColIndex] == "File2-Line1"));
				AssertLogLineExpected(file2, file2Line2, logRecords.Single(x => x.LogFields[messageColIndex] == "File2-Line2"));

				// Make sure we don't receive any events for logfile1:
				Thread.Sleep(500);
				Assert.Equal(0, ObservedEvents.Count);
			}

			[Fact]
			public void CanTailAllFilesInDirectory_AndAssignLogColumnsPerFileTailed()
			{
				var logFile1Columns = new[] { "A" };
				var logFile2Columns = new[] { "B" };
				Func<string, string[]> columnsProvider = file => (Path.GetFileName(file) == "logfile1.txt") ? logFile1Columns : logFile2Columns;

				var sut = new CsvLogTailer.CsvLogTailer();
				var settings = new CsvLogTailerSettings
					{
						FileOrDirectoryPath = logsDirectory,
						ColumnNamesProvider = columnsProvider
					};
				TailerSubscription = sut.Tail(settings).MaintainObservedEventsCollection(ObservedEvents);

				string file1 = CreateLogFile(logsDirectory, "logfile1.txt");
				string file2 = CreateLogFile(logsDirectory, "logfile2.txt");

				string file1Line1 = CreateLogLine(logMessage: "File1-Line1");
				string file2Line1 = CreateLogLine(logMessage: "File2-Line1");
				
				Write(file1Line1, file1);
				Write(file2Line1, file2);
				
				var logRecords = new List<LogRecord>
				{
					GetNext(ObservedEvents),
					GetNext(ObservedEvents)
				};

				int messageColIndex = Array.IndexOf(LogColumns, "Message");

				var logFile1Log = logRecords.Single(x => x.LogFields[messageColIndex] == "File1-Line1");
				Assert.Equal(logFile1Columns, logFile1Log.ColumnNames);

				var logFile2Log = logRecords.Single(x => x.LogFields[messageColIndex] == "File2-Line1");
				Assert.Equal(logFile2Columns, logFile2Log.ColumnNames);
			}
		}
	}
}
