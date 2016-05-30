using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EO.WebBrowser;

namespace EORepro
{
	public partial class Form1 : Form
	{
		private static readonly Random _rnd = new Random(1);

		private static readonly ConcurrentQueue<string> _events = new ConcurrentQueue<string>();

		private int _buddyCount;

		public Form1()
		{
			this.InitializeComponent();
		}

		private void Execute()
		{
			Task.Factory.StartNew(() =>
			{
				for (;;)
				{
					button1_Click(null, EventArgs.Empty);
					Thread.Sleep(500);
				}
			}, TaskCreationOptions.LongRunning);

			ThreadPool.QueueUserWorkItem(_ =>
			{
				var po = new ParallelOptions()
				{
					MaxDegreeOfParallelism = 6,
				};

				const string searchUrl = "https://www.google.com.tr/search?q={0}";
				int count = 0;

				Parallel.For(0, 20, po, i =>
				{
					var c = Interlocked.Increment(ref count);
					var url = string.Format(searchUrl, c);

					ThreadProcRandomMulti(url);
				});

				var instanceCount = 0;

				GC.Collect(2);
				GC.WaitForPendingFinalizers();
				GC.Collect(2);
				GC.WaitForPendingFinalizers();
				GC.Collect(2);
				GC.Collect(2);

				var urls = EOThreadBuddies.GetPressure4Details(out instanceCount);

				_events.Enqueue($"Undestroyed webviews: {instanceCount}");

				Application.DoEvents();

				// Here, we have disposed every WebView and ThreadRunner that we have created. There should not be any WebView instances in the heap at this point.
				Debugger.Break();
			});
		}

		private void ConfigureResourceHandler(WebView webView)
		{
			webView.NewWindow += WebView_NewWindow;

			var resourceHandler = new InterceptingResourceHandler();

			webView.RegisterResourceHandler(resourceHandler);
		}

		private void WebView_NewWindow(object sender, NewWindowEventArgs e)
		{
			_events.Enqueue("101");

			e.Accepted = false;
		}

		private int _counter;
		private int _count;

		private readonly ConcurrentQueue<EOThreadBuddies> _delayedDestroyQueue = new ConcurrentQueue<EOThreadBuddies>();

		private void ThreadProcRandomMulti(string url)
		{
			var searchUrl = url;
			var searchUrlCount = Interlocked.Increment(ref _counter);

			int i = 10;

			while (i >= 0)
			{
				Thread.Sleep((int)(1000 * _rnd.NextDouble()));

				var count = Interlocked.Increment(ref _count);

				var buddies = new EOThreadBuddies($"EO-Buddy-{count}");

				if (buddies.WebView == null)
				{
					_events.Enqueue("0");
				}

				ConfigureResourceHandler(buddies.WebView);

				WaitHandle loadedEvent = null;

				buddies.Send(() =>
				{
					var task = buddies.WebView.LoadUrl("http://edition.cnn.com");

					loadedEvent = task.GetDoneEvent(false);
				}, 3000);

				var isLoaded = loadedEvent.WaitOne(3000);


				buddies.Send(() =>
				{
					var task = buddies.WebView.LoadUrl(searchUrl);

					loadedEvent = task.GetDoneEvent(false);
				}, 3000);

				isLoaded = loadedEvent.WaitOne(3000);

				searchUrlCount = Interlocked.Increment(ref _counter);

				searchUrl = $"{url}v{searchUrlCount}";

				switch (_rnd.Next(5))
				{
					case 0:
						// recreate.
						buddies.Dispose();
						buddies.WebView.NewWindow -= WebView_NewWindow;

						count = Interlocked.Increment(ref _count);
						buddies = new EOThreadBuddies($"EO-Buddy-{count}");

						if (buddies.WebView == null)
						{
							_events.Enqueue("0");
						}

						ConfigureResourceHandler(buddies.WebView);

						buddies.Send(() =>
						{
							var task = buddies.WebView.LoadUrl("http://edition.cnn.com");

							loadedEvent = task.GetDoneEvent(false);
						}, 3000);

						isLoaded = loadedEvent.WaitOne(3000);

						break;
					case 1:
						// evalscript.
						var result = (int?)buddies.WebView.EvalScript(@"var i=1e6; while(i-- >0) { } 42");

						if (result != 42)
						{
							_events.Enqueue("1");
						}

						break;
					case 2:
						// queuescript
						var asyncCall = buddies.WebView.QueueScriptCall(@"var i=1e6; while(i-- >0) { } 43");

						asyncCall.WaitOne();

						if ((int)asyncCall.Result != 43)
						{
							_events.Enqueue("2");
						}

						break;
					case 3:
						// load something else.
						buddies.Send(() =>
						{
							var task = buddies.WebView.LoadUrl(searchUrl);

							loadedEvent = task.GetDoneEvent(false);
						}, 3000);

						isLoaded = loadedEvent.WaitOne(3000);

						if (!isLoaded)
						{
							_events.Enqueue("3");
						}

						break;
					case 4:
						// Delayed destroy.

						EOThreadBuddies delayedBuddies;

						while (_delayedDestroyQueue.TryDequeue(out delayedBuddies))
						{
							delayedBuddies.Dispose();
						}

						break;
				}

				buddies.Send(() =>
				{
					var task = buddies.WebView.LoadUrl(searchUrl);

					loadedEvent = task.GetDoneEvent(false);
				}, 3000);

				isLoaded = loadedEvent.WaitOne(3000);

				_delayedDestroyQueue.Enqueue(buddies);

				buddies.WebView.NewWindow -= WebView_NewWindow;

				i--;
			}

			EOThreadBuddies buddies2;

			while (_delayedDestroyQueue.TryDequeue(out buddies2))
			{
				buddies2.Dispose();
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			Execute();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (listBox1.InvokeRequired)
			{
				var action = new Action(() => button1_Click(sender, e));

				listBox1.BeginInvoke(action);

				return;
			}

			listBox1.Items.Clear();

			var items = _events.ToArray();

			listBox1.Items.Add($"Buddy Count: {_buddyCount}");
			listBox1.Items.Add($"Event Count: {items.Length}");
			listBox1.Items.Add($"Pressures: {EOThreadBuddies.GetPressures()}");

			foreach (var item in items)
			{
				listBox1.Items.Add(item);
			}
		}
	}
}