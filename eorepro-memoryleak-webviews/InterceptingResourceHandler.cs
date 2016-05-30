using System.Collections.Generic;
using System.Threading;
using EO.WebBrowser;

namespace EORepro
{
	/// <summary>
	/// <see cref="ResourceHandler"/> implementation intercepts DOM XSS requests and redirects them to <see cref="BrowserCacheManager"/>.
	/// </summary>
	public class InterceptingResourceHandler : ResourceHandler
	{
		private readonly List<Request> _pendingRequests = new List<Request>();

		public bool InitialResponseSent;

		public string InitialHtmlResponse = "<body onload=\"javascript:location.href='https://www.google.com'\"></body>";

		/// <summary>
		/// Checks the scheme of the given <paramref name="request"/>.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <returns><code>true</code> if scheme HTTP or HTTPS, otherwise <code>false</code>.</returns>
		public override bool Match(Request request)
		{
			return true;
		}

		private int _count;

		/// <summary>
		/// Caches the request and respond with the cached resource.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <param name="response">The response.</param>
		public override void ProcessRequest(Request request, Response response)
		{
			Thread.Sleep(300);

			var count = Interlocked.Increment(ref _count);

			if (count % 4 == 0)
			{
				response.StatusCode = 504;
			}
			else if (count % 5 == 0)
			{
				response.StatusCode = 403;
			}
			else
			{
				response.StatusCode = 200;
			}

			response.ContentType = "text/html";

			response.Headers["Ceche-Control"] = "No-Cache";
		}

		/// <summary>
		/// Gets a value indicating whether this instance has pending requests.
		/// </summary>
		public bool HasPendingRequests
		{
			get
			{
				lock (_pendingRequests)
				{
					return _pendingRequests.Count > 0;
				}
			}
		}

		/// <summary>
		/// Clears all pending requests.
		/// </summary>
		public void ClearPendingRequests()
		{
			lock (_pendingRequests)
			{
				_pendingRequests.Clear();
			}
		}
	}
}
