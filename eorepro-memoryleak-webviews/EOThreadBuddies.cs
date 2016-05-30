using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using EO.WebBrowser;
using EO.WebEngine;

namespace EORepro
{
	public class EOThreadBuddies : IDisposable
	{
		private readonly Guid _instanceId;

		private ThreadRunner _threadRunner;

		private WebView _webView;

		private readonly string _threadRunnerName;

		private readonly object _lock = new object();

		private const int EoLargeTimeout = 4 * 1000;

		private static readonly Thread _genesis;

		private static readonly BlockingCollection<Action> _genesisActions;

		private static CancellationTokenSource _tokenSource;

		private static readonly ConcurrentDictionary<Guid, WeakReference<ThreadRunner>> _threadRunners;

		private static readonly ManualResetEvent _genesisCompleted;

		public WebView WebView => _webView;

		static EOThreadBuddies()
		{
			_threadRunners = new ConcurrentDictionary<Guid, WeakReference<ThreadRunner>>();
			_genesisActions = new BlockingCollection<Action>();
			_tokenSource = new CancellationTokenSource();
			_genesisCompleted = new ManualResetEvent(false);
			_genesis = new Thread(GenesisWorker) { IsBackground = true, Name = "GW" };

			//EO.Base.Runtime.Exception += Runtime_Exception;

			_genesis.Start();
		}

		private static void Runtime_Exception(object sender, EO.Base.ExceptionEventArgs e)
		{
			MessageBox.Show("handled");
		}

		private static void GenesisWorker()
		{
			GenesisWorkerImpl();
		}

		private static void GenesisWorkerImpl()
		{
			try
			{
				var tokenSource = _tokenSource;

				var actions = _genesisActions.GetConsumingEnumerable(tokenSource.Token);

				foreach (var action in actions)
				{
					try
					{
						action();
					}
					catch (Exception ex)
					{
						MessageBox.Show(ex.ToString());
						//Debugger.Break();
					}
				}
			}
			catch (OperationCanceledException)
			{
				// NOOP.
			}
			finally
			{
				_genesisCompleted.Set();
			}

			// Assume the application is shutting down or the current AppDomain is being unloaded, hence do not process rest of the _genesisActions.
		}

		public EOThreadBuddies(string threadRunnerName, BrowserOptions options = null)
		{
			_instanceId = Guid.NewGuid();

			_threadRunnerName = threadRunnerName;

			using (var created = new ManualResetEvent(false))
			{
				_genesisActions.Add(() =>
				{
					try
					{
						if (_tokenSource.IsCancellationRequested)
						{
							return;
						}

						_threadRunner = new ThreadRunner(threadRunnerName);
						_threadRunners.TryAdd(_instanceId, new WeakReference<ThreadRunner>(_threadRunner));

						_webView = _threadRunner.CreateWebView(options);

						if (_webView == null)
						{
							Debugger.Break();
						}

					}
					finally
					{
						created.Set();
					}
				});

				created.WaitOne();
			}
		}

		public void Dispose()
		{
			if (_tokenSource.IsCancellationRequested)
			{
				return;
			}

			using (var disposed = new ManualResetEvent(false))
			{
				lock (_lock)
				{
					_genesisActions.Add(() =>
					{
						try
						{
							_webView.Destroy();

							_threadRunner.Dispose();

							WeakReference<ThreadRunner> phony;

							_threadRunners.TryRemove(_instanceId, out phony);
						}
						finally
						{
							disposed.Set();
						}
					});
				}

				disposed.WaitOne();
			}
		}

		internal bool Send(Func<object> action)
		{
			if (_tokenSource.IsCancellationRequested)
			{
				return false;
			}

			var eoAction = new EO.Base.ActionWithResult(action);

			bool isSignaled;

			lock (_lock)
			{
				_threadRunner.Send(eoAction, EoLargeTimeout, out isSignaled);
			}

			return isSignaled;
		}

		internal bool Send(Action action, int timeout)
		{
			if (_tokenSource.IsCancellationRequested)
			{
				return false;
			}

			var eoAction = new EO.Base.Action(action);

			bool result;

			lock (_lock)
			{
				result = _threadRunner.Send(eoAction, timeout);
			}

			return result;
		}

		public static string GetPressures()
		{
			return $"{GetPressure1()}, {GetPressure2()}, {GetPressure3()}, {GetPressure4()}";
		}

		private static int GetPressure1()
		{
			try
			{
				var wType = typeof(EO.Base.Action).Assembly.GetType("EO.Internal.w");

				var iStaticField = wType.GetField("i", BindingFlags.NonPublic | BindingFlags.Static);

				var iValue = (ICollection)iStaticField.GetValue(null);

				var windowCount = iValue.Count;

				return windowCount;
			}
			catch (Exception)
			{
				// Ignore everything.
			}

			return 0;
		}

		private static int GetPressure2()
		{
			try
			{
				var bStaticField = typeof(WebView).GetField("b", BindingFlags.NonPublic | BindingFlags.Static);

				var bValue = (List<WebView>)bStaticField.GetValue(null);

				return bValue.Count;
			}
			catch (Exception)
			{
				// Ignore everything.
			}

			return 0;
		}

		private static int GetPressure3()
		{
			try
			{
				var xrType = typeof(EO.Base.Action).Assembly.GetType("EO.Internal.xr");

				var bStaticField = xrType.GetField("b", BindingFlags.NonPublic | BindingFlags.Static);

				var bValue = (ICollection)bStaticField.GetValue(null);

				var cleanupActionCount = bValue.Count;

				return cleanupActionCount;
			}
			catch (Exception)
			{
				// Ignore everything.
			}

			return 0;
		}

		private static int GetPressure4()
		{
			int ret;

			GetPressure4Details(out ret);

			return ret;
		}

		public static IEnumerable<string> GetPressure4Details(out int webViewInstances)
		{
			webViewInstances = 0;

			List<string> ret = new List<string>();

			try
			{
				var h6Type = typeof(EO.Base.Action).Assembly.GetType("EO.Internal.h6");

				var bStaticField = h6Type.GetField("b", BindingFlags.NonPublic | BindingFlags.Static);

				var bValue = (ICollection)bStaticField.GetValue(null);

				var h6aType = typeof(EO.Base.Action).Assembly.GetType("EO.Internal.h6+a");
				var openDic = typeof(Dictionary<,>);
				var dictType = openDic.MakeGenericType(typeof(int), h6aType);

				var dictValues = (IEnumerable)dictType.GetProperty("Values").GetValue(bValue);

				var h6a_bField = h6aType.GetField("b");
				var h6a_cField = h6aType.GetField("c");

				var cmType = typeof(WebView).Assembly.GetType("EO.Internal.cm");
				var cm_eField = cmType.GetField("e", BindingFlags.Instance | BindingFlags.NonPublic);

				var webViews = new HashSet<WebView>();

				lock (h6Type)
				{
					foreach (var item in dictValues)
					{
						var h6a_b = h6a_bField.GetValue(item);
						var h6a_c = h6a_cField.GetValue(item);

						if (cmType.IsInstanceOfType(h6a_b))
						{
							var webView = (WebView)cm_eField.GetValue(h6a_b);
							webViews.Add(webView);
							ret.Add(webView.Url);

							webViewInstances++;
						}

						if (cmType.IsInstanceOfType(h6a_c))
						{
							var webView = (WebView)cm_eField.GetValue(h6a_c);
							webViews.Add(webView);
							ret.Add(webView.Url);

							webViewInstances++;
						}
					}
				}

				webViewInstances = webViews.Count;

				return ret;
			}
			catch (Exception)
			{
				// Ignore everything.
			}

			return ret;
		}
	}
}
