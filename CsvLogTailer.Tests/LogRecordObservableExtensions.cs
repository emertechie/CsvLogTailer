using System;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Linq;

namespace CsvLogTailing.Tests
{
	public static class LogRecordObservableExtensions
	{
		public static IDisposable MaintainObservedEventsCollection(
			this IObservable<LogRecord> tailedEvents,
			BlockingCollection<Notification<LogRecord>> observedEvents)
		{
			return tailedEvents
				.Materialize()
				.Subscribe(
					observedEvents.Add,
					err =>
					{
						observedEvents.CompleteAdding();
						throw err;
					});
		}
	}
}