using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Accessibility;

namespace WebpageWaiter.WebAutoType
{
	internal class FirefoxBrowserUrlReader : BrowserUrlReader
	{
		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		private const int FocusStealTimeout = 2000; //ms

		public FirefoxBrowserUrlReader(IntPtr hwnd): base(hwnd)
		{
		}

		public override string GetBrowserFocusUrl(out bool passwordFieldFocussed)
		{
			var url = base.GetBrowserFocusUrl(out passwordFieldFocussed);
			if (url == null)
			{
				EnsureFirefoxAccessibilityEnabled();
				return base.GetBrowserFocusUrl(out passwordFieldFocussed);
			}
			return url;
		}

		public override string GetBrowserFocusUrlWithInfo(out string title, out string selectedText)
		{
			var url = base.GetBrowserFocusUrlWithInfo(out title, out selectedText);
			if (url == null)
			{
				EnsureFirefoxAccessibilityEnabled();
				return base.GetBrowserFocusUrlWithInfo(out title, out selectedText);
			}
			return url;
		}

		private void EnsureFirefoxAccessibilityEnabled()
		{
			// Firefox may not have had a11y enabled, so if it doesn't find the document, trigger it by setting making the window lose and regain focus (note don't do this for GetDocument basic, as this might be used for URLs where the window is not foreground)
			using (var focusStealer = new Form
			{
				Opacity = 0, // Invisible
				ShowInTaskbar = false,
				FormBorderStyle = FormBorderStyle.FixedToolWindow, // Prevents showing in alt+tab
			})
			{
				focusStealer.Show();
				var timeout = new Stopwatch();
				timeout.Start();
				while (GetForegroundWindow() != focusStealer.Handle &&
					   timeout.ElapsedMilliseconds < FocusStealTimeout)
				{
					focusStealer.Activate();
					Thread.Sleep(10);
				}
				focusStealer.Close();
			}
		}

		protected internal override IAccessible GetDocument()
		{
			var application = AccessibleObjectHelper.FindChild(AccessibleObjectHelper.GetAccessibleObjectFromWindow(mHwnd),
				role: AccessibleRole.Application);

			// Sometimes there are multiple groupings, indistinguishable, so try each to find the property page below it
			var groupings = AccessibleObjectHelper.GetChildren(application).Where(child =>
				AccessibleObjectHelper.AccessibleObjectMatchesConditions(child,
					role: AccessibleRole.Grouping,
						hasNotState: AccessibleStates.Invisible | AccessibleStates.Focusable));

			foreach (var grouping in groupings)
			{
                var propertyPage = AccessibleObjectHelper.FindChild(grouping,
                        role: AccessibleRole.PropertyPage,
                        hasNotState: AccessibleStates.Offscreen /*(inactive tab)*/);

				if (propertyPage != null)
				{
                    var browser = AccessibleObjectHelper.FindChild(propertyPage, hasState: AccessibleStates.Focusable);
                    return AccessibleObjectHelper.FindChild(browser, role: AccessibleRole.Document);
                }
            }

			return null;
		}
	}
}