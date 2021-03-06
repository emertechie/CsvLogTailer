using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvLogTailing.Bookmarks;
using FParsec;

namespace CsvLogTailing
{
	public class CsvLogTailer
	{
		private readonly TimeSpan logDirectoryPollTimeSpan = TimeSpan.FromSeconds(30);
		private readonly TimeSpan filePollTimeSpan = TimeSpan.FromSeconds(0.5);

		private readonly Subject<Exception> exceptionsSubject;
		protected readonly ISubject<Exception, Exception> SyncedExceptionsSubject;

		private readonly object parsingLock = new object();
		private int logsReadSinceLastGarbageCollect = 0;
		private int? forceMemoryCollectionThreshold;
		
		public CsvLogTailer(int? forceMemoryCollectionThreshold = null)
		{
			this.forceMemoryCollectionThreshold = forceMemoryCollectionThreshold;

			exceptionsSubject = new Subject<Exception>();
			// Exceptions can be raised concurrently on different threads, so protect access to subject to ensure sequential notifications:
			SyncedExceptionsSubject = Subject.Synchronize(exceptionsSubject);
		}

		public IObservable<Exception> Exceptions
		{
			get { return exceptionsSubject; }
		}

		public IObservable<LogRecord> Tail(CsvLogTailerSettings settings)
		{
			return Tail(settings, new NullLogFileBookmarkRepository());
		}

		public IObservable<LogRecord> Tail(CsvLogTailerSettings settings, ILogFileBookmarkRepository logFileBookmarkRepository)
		{
			if (settings == null) throw new ArgumentNullException("settings");
			if (logFileBookmarkRepository == null) throw new ArgumentNullException("logFileBookmarkRepository");

			bool isADirectory = Directory.Exists(settings.FileOrDirectoryPath);

			IObservable<LogRecord> logRecordsObs = isADirectory
				? GetAllFileChangesForDirectory(settings, logFileBookmarkRepository)
					.Merge()
				: GetFileChanges(
					settings.FileOrDirectoryPath,
					settings.Encoding,
					GetColumnsForFile(settings.FileOrDirectoryPath, settings),
					settings.DateTimeColumnIndex,
					logFileBookmarkRepository);

			return Observable.Create<LogRecord>(observer =>
			{
				var sharedObservable = logRecordsObs.Publish();

				var subscription1 = sharedObservable.Subscribe(observer);

				var subscription2 = sharedObservable
					.GroupBy(x => x.FilePath)
					.Subscribe(group =>
						{
							group
								.SampleResponsive(settings.BookmarkRepositoryUpdateFrequency)
								.Subscribe(logRec =>
									{
										try
										{
											logFileBookmarkRepository.AddOrUpdate(new LogFileBookmark(logRec.FilePath, logRec.LogDateTime));
										}
										catch (Exception bookmarkException)
										{
											SyncedExceptionsSubject.OnNext(bookmarkException);
										}
									});
						});

				return new CompositeDisposable(sharedObservable.Connect(), subscription1, subscription2);
			});
		}

		private IObservable<LogRecord> GetFileChanges(
			string filePath,
			Encoding encoding,
			string[] possiblyNullColumnNames,
			int dateTimeColumnIndex,
			ILogFileBookmarkRepository logFileBookmarkRepository)
		{
			if (possiblyNullColumnNames != null && dateTimeColumnIndex >= possiblyNullColumnNames.Length)
				throw new ArgumentOutOfRangeException("dateTimeColumnIndex", "DateTime column index is greater than number of columns");

			var lastKnownPosition = logFileBookmarkRepository.Get(filePath);

			return Observable.Create<LogRecord>(observer =>
			{
				var disposable = new CompositeDisposable();
				var cancellationTokenSource = new CancellationTokenSource();

				int sharingExceptions = 0;

				Task fileWatcherTask = Task.Factory.StartNew(() =>
					{
						do
						{
							try
							{
								TailFile(filePath, encoding, possiblyNullColumnNames, dateTimeColumnIndex, observer, cancellationTokenSource, lastKnownPosition);

								sharingExceptions = 0;
							}
							catch (FileNotFoundException)
							{
								WaitUntilFileCreated(filePath, cancellationTokenSource);
							}
							catch (IOException ioex)
							{
								if (ioex.Message.Contains("because it is being used by another process") && ++sharingExceptions < 10)
									Thread.Sleep(250);
								else
									throw;
							}
							catch (Exception ex)
							{
								observer.OnError(ex);
								throw;
							}
						}
						while (!cancellationTokenSource.IsCancellationRequested);
					},
					TaskCreationOptions.LongRunning);

				// Make sure any Task exception is observed
				fileWatcherTask.ContinueWith(
					t => observer.OnError(new Exception("Error while tailing file. See inner exception for more details", t.Exception)),
					TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

				var signalEnd = Disposable.Create(() =>
				{
					cancellationTokenSource.Cancel();
					fileWatcherTask.Wait(TimeSpan.FromSeconds(Debugger.IsAttached ? 120 : 2));
				});

				disposable.Add(signalEnd);
				return disposable;
			});
		}

		private static string[] GetColumnsForFile(string filePath, CsvLogTailerSettings settings)
		{
			return settings.ColumnNamesProvider != null ? settings.ColumnNamesProvider(filePath) : null;
		}

		private IObservable<IObservable<LogRecord>> GetAllFileChangesForDirectory(CsvLogTailerSettings settings, ILogFileBookmarkRepository logFileBookmarkRepository)
		{
			return Observable.Create<IObservable<LogRecord>>(observer =>
			{
				var fileTailerSubscriptions = new Dictionary<string, IDisposable>();

				var directoryChangesSubscription = GetDirectoryChanges(settings.FileOrDirectoryPath, settings.DirectoryFilter)
					.Subscribe(change =>
					{
						if (settings.FileNameExcludeRegex != null && settings.FileNameExcludeRegex.IsMatch(Path.GetFileName(change.Path)))
							return;

						if (change.ChangeType == FileTailingChangeType.StartTailing)
						{
							string[] columnsForFile = GetColumnsForFile(change.Path, settings);

							IObservable<LogRecord> fileChanges = GetFileChanges(
								change.Path,
								settings.Encoding,
								columnsForFile,
								settings.DateTimeColumnIndex,
								logFileBookmarkRepository);

							// Putting a thin wrapper around the 'fileChanges' observable so we can immediately dispose the subscription for individual files
							// and free up resources associated with it. Otherwise, they may not get freed until program shutdown.
							IObservable<LogRecord> wrappedFileChanges = Observable.Create<LogRecord>(fileChangesObserver =>
								{
									IDisposable subscription = fileChanges.Subscribe(fileChangesObserver);
									fileTailerSubscriptions.Add(change.Path, subscription);
									return () => { };
								});
							observer.OnNext(wrappedFileChanges);
						}
						else
						{
							var subscription = fileTailerSubscriptions[change.Path];
							subscription.Dispose();
							fileTailerSubscriptions.Remove(change.Path);
						}
					});

				var stopWatchingFileChanges = Disposable.Create(() =>
				{
					foreach (IDisposable fileChangesSubscription in fileTailerSubscriptions.Values)
						fileChangesSubscription.Dispose();
				});

				return new CompositeDisposable(directoryChangesSubscription, stopWatchingFileChanges);
			});
		}

		private IObservable<FileTailingChange> GetDirectoryChanges(string directoryPath, string directoryFilter)
		{
			string filter = directoryFilter ?? "*.*";
			var watcher = new FileSystemWatcher(directoryPath, filter)
				{
					// For some reason you need to specify this filter for delete notifications to work...
					NotifyFilter = NotifyFilters.FileName
				};
			watcher.Error += (sender, args) =>
				{
					var exception = args.GetException();
					SyncedExceptionsSubject.OnNext(new Exception("Error from FileSystemWatcher: " + exception.Message, exception));
				};

			var trackedPaths = new ConcurrentDictionary<string, bool>();

			return Observable.Create<FileTailingChange>(observer =>
				{
					var fswLock = new object();

					var syncedObserver = Observer.Synchronize(observer);
					IObservable<FileTailingChange> fileSystemWatcherChanges = GetFileSystemWatcherChanges(watcher)
						.Where(x =>
							{
								lock (fswLock)
								{
									bool ignored;
									return x.ChangeType == FileTailingChangeType.StartTailing
										       ? trackedPaths.TryAdd(x.Path, true)
										       : trackedPaths.TryRemove(x.Path, out ignored);
								}
							});

					watcher.EnableRaisingEvents = true;

					var cts = new CancellationTokenSource();
					Task.Factory.StartNew(() =>
						{
							do
							{
								var files = Directory.EnumerateFiles(directoryPath, filter);

								lock (fswLock)
								{
									foreach (string file in files)
									{
										if (trackedPaths.TryAdd(file, true))
											syncedObserver.OnNext(new FileTailingChange(file, FileTailingChangeType.StartTailing));
									}
								}
								cts.Token.WaitHandle.WaitOne(logDirectoryPollTimeSpan);
							}
							while (!cts.IsCancellationRequested);
						},
						cts.Token)
						.ContinueWith(
							t => observer.OnError(t.Exception),
							TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

					var stopFswDisposable = Disposable.Create(() => watcher.EnableRaisingEvents = false);
					var fileWatcherSubscription = fileSystemWatcherChanges.Subscribe(syncedObserver);
					var stopTaskDisposable = Disposable.Create(cts.Cancel);
					return new CompositeDisposable(stopFswDisposable, fileWatcherSubscription, stopTaskDisposable);
				});
		}

		private static IObservable<FileTailingChange> GetFileSystemWatcherChanges(FileSystemWatcher watcher)
		{
			var created = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
				handler => watcher.Created += handler,
				handler => watcher.Created -= handler)
				.Select(x => new[] {new FileTailingChange(x.EventArgs.FullPath, FileTailingChangeType.StartTailing)});

			// TODO: We won't get delete events for log files we have open. Noticed that if you delete file in Win Explorer and then refreshed the 
			// directory, the file reappeared. See: http://superuser.com/questions/105786/windows-7-files-reappear-after-deletion
			// Will probably need to periodically close file streams and try to reopen (hopefully file is free to be deleted then)
			var deleted = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
				handler => watcher.Deleted += handler,
				handler => watcher.Deleted -= handler)
				.Select(x => new[] {new FileTailingChange(x.EventArgs.FullPath, FileTailingChangeType.StopTailing)});

			var renamed = Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
				handler => watcher.Renamed += handler,
				handler => watcher.Renamed -= handler)
				.Select(x => new[]
					{
						new FileTailingChange(x.EventArgs.OldFullPath, FileTailingChangeType.StopTailing),
						new FileTailingChange(x.EventArgs.FullPath, FileTailingChangeType.StartTailing)
					});

			return Observable.Merge(created, deleted, renamed).SelectMany(x => x);
		}

		private void WaitUntilFileCreated(string filePath, CancellationTokenSource cancellationTokenSource)
		{
			var fileCreated = new ManualResetEventSlim(false);
			cancellationTokenSource.Token.Register(fileCreated.Set);

			// Note: FileSystemWatcher just doesn't work which is why it's not used here
			while (!fileCreated.Wait(TimeSpan.FromSeconds(1)))
			{
				if (File.Exists(filePath))
					fileCreated.Set();
			}
		}

		private void TailFile(
			string filePath,
			Encoding encoding,
			string[] possiblyNullColumnNames,
			int dateTimeColumnIndex,
			IObserver<LogRecord> observer,
			CancellationTokenSource cancellationTokenSource,
			LogFileBookmark lastKnownPosition = null)
		{
			using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			{
				DateTime minLogDateTimeFilter = DateTime.MinValue;
				if (lastKnownPosition != null)
				{
					minLogDateTimeFilter = lastKnownPosition.LogDateTime;
				}

				long lastStreamPos = fileStream.Position;

				do
				{
					while (!cancellationTokenSource.IsCancellationRequested && fileStream.Length != lastStreamPos)
					{
						ReadNext(filePath, fileStream, encoding, possiblyNullColumnNames, dateTimeColumnIndex, next =>
							{
								// Note: Deliberate use of '>=' condition below because date time format used in log file may not have enough
								// resolution for high frequency logs. Seeking to last position should give you exact starting point anyway.
								// Filtering by date is just an additional failsafe to prevent outputting tons of old logs again
								if (next.LogDateTime >= minLogDateTimeFilter)
									observer.OnNext(next);
							});

						if (fileStream.Position == lastStreamPos)
							break;

						lastStreamPos = fileStream.Position;
					}

					if (cancellationTokenSource.IsCancellationRequested)
						break;

					Thread.Sleep(filePollTimeSpan);
				}
				while (!cancellationTokenSource.IsCancellationRequested);
			}
		}

		private void ReadNext(string filePath, Stream stream, Encoding encoding, string[] possiblyNullColumnNames, int dateTimeColumnIndex, Action<LogRecord> action)
		{
			/*
			 * TODO:
			 * 
			 * - What about if file is archived off in middle of trying to recover from exception? Will that really screw things up
			 * - Maybe record stream end position on entering method and ensure we never read past that somehow?
			 * 
			 */

			// Reset stream position if file is truncated (or larger file is overwritten with smaller file)
			if (stream.Position > stream.Length)
				stream.Position = 0;

			var originalStreamPosition = stream.Position;
			Exception lastException = null;

			bool leaveOpen = true;
			var charStream = CreateCharStream(stream, leaveOpen, encoding);

			int skippedLines = 0;

			try
			{
				do
				{
					try
					{
						var parser = new CsvParser.CsvParser('|');
						
						// Big-ass lock. Necessary to prevent temporary memory explosion on startup if there are lots of existing logs to be read.
						// Clearly there must be a better way, but has to do for now. Also protects access to logsReadSinceLastGarbageCollect and hence calls to GC.Collect
						lock (parsingLock)
						{
							var nextRecords = parser.ParseCharStream(charStream)
								.Where(fields => fields.Any() && !String.IsNullOrWhiteSpace(fields[0])) // <<< TODO: Can remove this when parser fixed
								.Where(fields => !String.IsNullOrWhiteSpace(fields[dateTimeColumnIndex]))
								.Select(fields => new LogRecord(filePath, DateTime.Parse(fields[dateTimeColumnIndex]), fields, possiblyNullColumnNames));

							foreach (var nextRecord in nextRecords)
							{
								action(nextRecord);
								++logsReadSinceLastGarbageCollect;
							}

							if (forceMemoryCollectionThreshold.HasValue && logsReadSinceLastGarbageCollect >= forceMemoryCollectionThreshold.Value)
							{
								GC.Collect();
								logsReadSinceLastGarbageCollect = 0;
							}
						}

						break;
					}
					catch (Exception exception)
					{
						if (lastException == null)
						{
							lastException = exception;
							SyncedExceptionsSubject.OnNext(lastException);
						}

						// Reset everything and...
						stream.Position = originalStreamPosition;
						charStream.Dispose();
						charStream = CreateCharStream(stream, leaveOpen, encoding);

						try
						{
							// ... try again from next line down
							++skippedLines;
							for (int i = 0; i < skippedLines; i++)
								charStream.SkipRestOfLine(skipNewline: true);

							// string whereAmI = charStream.PeekString(50);

							if (charStream.IsEndOfStream)
								return;
						}
						catch (Exception)
						{
							// TODO: Is this recoverable?

							throw;
						}
					}
				}
				while (true);
			}
			finally
			{
				charStream.DisposeIfNotNull();
			}
		}

		private CharStream<CsvParserModule.CsvParserState> CreateCharStream(Stream stream, bool leaveOpen, Encoding encoding)
		{
			return new CharStream<CsvParserModule.CsvParserState>(stream, leaveOpen, encoding)
			{
				UserState = new CsvParserModule.CsvParserState('|')
			};
		}

		private enum FileTailingChangeType
		{
			StartTailing,
			StopTailing
		}

		private class FileTailingChange
		{
			private readonly string path;
			private readonly FileTailingChangeType changeType;

			public FileTailingChange(string path, FileTailingChangeType changeType)
			{
				this.path = path;
				this.changeType = changeType;
			}

			public string Path
			{
				get { return path; }
			}

			public FileTailingChangeType ChangeType
			{
				get { return changeType; }
			}
		}

		private class NullLogFileBookmarkRepository : ILogFileBookmarkRepository
		{
			public LogFileBookmark Get(string filePath)
			{
				return null;
			}

			public void AddOrUpdate(LogFileBookmark bookmark)
			{
			}
		}
	}
}