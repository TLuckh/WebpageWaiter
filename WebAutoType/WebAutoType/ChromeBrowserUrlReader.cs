using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Accessibility;

namespace WebAutoType
{
	internal class ChromeBrowserUrlReader : BrowserUrlReader
	{
		// When Chrome enables accessibility, it takes a little while to enable. This value controls how long we wait to try again.
		private static readonly TimeSpan ChromeRePollDelay = TimeSpan.FromMilliseconds(500);

		// When Edgeium enables accessibility, it also takes a little while to enable, but we don't get an event, so timeout after this long
		private static readonly TimeSpan WaitForAccessibility = TimeSpan.FromSeconds(3);
		internal static bool IsEmbeddedIEFrame(IntPtr hwnd)
		{
			// Try embedded IE mode in Edgium
			return FindDescendantWindows(hwnd, "IEFrame").Any();
		}

		public ChromeBrowserUrlReader(IntPtr hwnd) : base(hwnd)
		{
		}

		protected override IAccessible GetDocument()
		{
			var chromeRenderHwnd = FindDescendantWindows(mHwnd, "Chrome_RenderWidgetHostHWND").FirstOrDefault();
			if (chromeRenderHwnd == IntPtr.Zero)
			{
				return null;
			}

			// Chrome only enables accessibility if it gets a top-level IAccessible request, so let's make one first
			var _ = AccessibleObjectHelper.GetAccessibleObjectFromWindow(mHwnd).accName;

			IAccessible windowDoc;
			var time = new Stopwatch();
			time.Start();
			while ((windowDoc = TryGetAccessibleDocFromRenderWindow(chromeRenderHwnd)) == null && time.Elapsed < WaitForAccessibility)
			{
				Thread.Sleep(ChromeRePollDelay);
			}

			return windowDoc;
		}

		
		private static IAccessible TryGetAccessibleDocFromRenderWindow(IntPtr chromeRenderHwnd)
		{
			try
			{
				var windowDoc = AccessibleObjectHelper.FindChild(AccessibleObjectHelper.GetAccessibleObjectFromWindow(chromeRenderHwnd), role: AccessibleRole.Document);
				if (windowDoc != null && windowDoc.accFocus is IAccessible)
				{
					return windowDoc;
				}
			}
			catch (System.Runtime.InteropServices.COMException)
			{
			}
			return null;
		}

		public ChromeAccessibilityWinEventHook ChromeAccessibilityWinEventHook;

		public override string GetBrowserFocusUrl(out bool passwordFieldFocussed)
		{
			if (ChromeAccessibilityWinEventHook != null)
			{
				ChromeAccessibilityWinEventHook.EventReceived = false;
			}
			var url = base.GetBrowserFocusUrl(out passwordFieldFocussed);
			if (ChromeAccessibilityWinEventHook != null && ChromeAccessibilityWinEventHook.EventReceived)
			{
				// If Chrome accessibility received an event, then Chrome has just turned on accessibility, so re-query for the focused element now that Chrome will actually provide it.
				Thread.Sleep(ChromeRePollDelay);
				url = base.GetBrowserFocusUrl(out passwordFieldFocussed);
			}
			return url;
		}
	}
}