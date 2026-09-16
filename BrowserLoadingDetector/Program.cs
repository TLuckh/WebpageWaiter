using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using MoreLinq;
using Debug = System.Diagnostics.Debug;

namespace BrowserLoadingDetector
{
    public class Program
    {
        /// <summary>
        /// Starte die Methode, hole dann innerhalb von 1s eine Webpage in den Vordergrund, und die Methode gibt zurück,
        /// ob die Seite fertig geladen hat (basierend auf dem Zustand des Neu-Lade-Buttons).
        /// Nutze im Ordner Tools die beiden Teile, um eine Test-Webseite, die ewig lädt, zu testen.
        ///
        /// Funktionsweise ist wie folgt:
        /// Per FlaUI wird die Schaltfläche des Buttons bestimmt
        /// (klappt nur bei den Browsern, für die ich es gehardcodet habe, dort aber vermutlich langfristig).
        /// (Firefox macht dies als einziger Browser ordentlich - nur Firefox gibt eine feste AutomationId zurück, die je nach geladen/am laden unterschiedlich ist;
        ///  Chromium dagegen hat es atm als view_1003, sowohl beim Laden als auch nach Laden, und der String ist vermutlich nicht langfristig stabil)
        ///
        /// Von der Schaltfläche wird dann ein Screenshot gemacht, dieser dann so gemacht, dass der Vordergrund weiß ist,
        /// und dann darauf per Hough-Transformation nach einem Kreis bzw. zwei Linien gesucht.
        ///
        /// Sind zwei Linien der beste Match, war es ein X, also Webseite lädt noch.
        /// Andernfalls war es der Neu-Laden-Kreis, d.h. Webseite ist fertig geladen. 
        /// 
        /// </summary>
        /// <param name="args"></param>
        private static void UsageExample()
        {
            Thread.Sleep(1000);
            AutomationElement button = BrowserLoadingButtons.GetLoadingButton();

            if (button == null)
            {
                Console.WriteLine("Button nicht gefunden");
                return;
            }

            BitmapMat buttonState = GetLoadingButtonBitmap(button);
            if (buttonState == null)
            {
                Console.WriteLine("Couldn't get button.");
                return;
            }

            bool isLoadingFinished = HasLoadingFinished(buttonState);
            Console.WriteLine(isLoadingFinished ? "Website has loaded." : "Website is still loading.");
        }

        public static bool HasLoadingFinished(BitmapMat buttonState)
        {
            // Find out which is foreground and which is background
            // ReSharper disable once InvokeAsExtensionMember
            BitmapMat inputMat = buttonState;
            int whitePixels = MoreEnumerable.Cartesian(Enumerable.Range(0, inputMat.Width),
                                                       Enumerable.Range(0, inputMat.Height),
                                                       (x, y) => inputMat[x, y])
                                            .Aggregate(0, (x, y) => x + y);

            // White pixels should be foreground, that is, in the minority
            if (whitePixels > 1.0 / 2 * inputMat.Width * inputMat.Height)
            {
                buttonState.Invert();
            }


            /*
             * We now test two scenarios:
             * a.	Try to  overlay two freely selectable black straight lines with a pixel thickness of 1 onto the image.
             * b.	Try to  overlay a freely selectable black circle with a pixel thickness of 1 onto the image
             *
             * We then choose whichever method covers more black pixels.
             */
            List<((int x, int y), double radius, int hits)> circles =
                HoughTransform.HoughCircles(buttonState, 1);


            ((int x, int y), double radius, int hits) bestCircleCoverage = circles.Maxima(x => x.hits).FirstOrDefault();


            (int y_coord, double angle_radians, int hits)[] lines =
                HoughTransform.HoughLinesWithXEquals0(buttonState, Math.PI / 180, 1);
            (int y_coord, double angle_radians, int hits) bestLine = lines.Maxima(x => x.hits).FirstOrDefault();
            CountPointsLine.RemoveLine(buttonState, (0, bestLine.y_coord), bestLine.angle_radians, 1);
            (int y_coord, double angle_radians, int hits)[] lines2 =
                HoughTransform.HoughLinesWithXEquals0(buttonState, Math.PI / 180, 1);
            (int y_coord, double angle_radians, int hits) bestLine2 = lines2.Maxima(x => x.hits).FirstOrDefault();

            int bestLinesCoverage = bestLine2.hits + bestLine.hits;

            Debug.WriteLine(bestCircleCoverage.hits);
            Debug.WriteLine(bestLinesCoverage);
            return bestCircleCoverage.hits > bestLinesCoverage;
        }

        public static BitmapMat GetLoadingButtonBitmap(AutomationElement button)
        {
            BitmapMat buttonState = null;
            for (int i = 0; i < 10; i++)
                try
                {
                    buttonState = (BitmapMat)button.Capture();
                    break;
                }
                catch
                {
                    Thread.Sleep(100);
                    // ToDo: Log error if i==10. 
                }


            return buttonState;
        }
    }


    /// <summary>
    ///     Class with primary method GetLoadingButton, which tries to find the loading button of the chosen browser window.
    /// </summary>
    public class BrowserLoadingButtons
    {
        // AutmationId scheint bei Chromium "view_1003" zu sein, und am eindeutigsten als Identifier. Gleichzeitig als interner String vermutlich nicht über Releases stabil
        // Hierachie-Position in UIA3: /Window[2]/Pane[2]/Pane/Pane/Pane[2]/Pane[1]/ToolBar/Button[3]
        public static readonly BrowserLoadingButtons ChromeBraveEdge =
            new BrowserLoadingButtons("Chrome", "ReloadButton", "", "", ControlType.Button);

        // Hierachie-Position in UIA3: /Window[2]//ToolBar[3]/Button[4]  ; nav-bar --> reload-button
        public static readonly BrowserLoadingButtons Firefox =
            new BrowserLoadingButtons("Gecko", "toolbarbutton-1", "reload-button", "stop-button", ControlType.Button);

        // Hierachie-Position in UIA3: /Window[5]/Pane[2]/Pane/Pane/Group/Pane/Pane/Pane/Pane[1]/Group/Group/Group/ToolBar/Pane[1]/Pane[1]/Button[3]
        public static readonly BrowserLoadingButtons Opera =
            new BrowserLoadingButtons("Chrome", "ReloadStopButton", "", "", ControlType.Button);

        private BrowserLoadingButtons
            (
            string      frameworkId,
            string      className,
            string      automationIdLoaded,
            string      automationIdLoading,
            ControlType controlType
            )
        {
            FrameworkId         = frameworkId;
            ClassName           = className;
            AutomationIdLoaded  = automationIdLoaded;
            AutomationIdLoading = automationIdLoading;
            ControlType         = controlType;
        }

        private string      FrameworkId         { get; }
        private string      ClassName           { get; }
        private string      AutomationIdLoaded  { get; }
        private string      AutomationIdLoading { get; }
        private ControlType ControlType         { get; }

        /// <summary>
        ///     Tries to find the loading button of the chosen browser window.
        ///     Returns null on failure. Returns the button AutomationElement on success
        /// </summary>
        /// <param name="delayMs"></param>
        /// <param name="processId">The process ID of the target window, or 0 if the foreground window should be used.</param>
        /// <returns></returns>
        public static AutomationElement GetLoadingButton(int delayMs = 50, uint processId = 0)
        {
            // If no processId is given, we use the foreground window
            Thread.Sleep(delayMs);
            if (processId == 0)
            {
                IntPtr hWnd = MarshallingMethods.GetForegroundWindow();
                MarshallingMethods.GetWindowThreadProcessId(hWnd, out processId);
            }

            Application app = Application.Attach((int)processId);


            using (var auto = new UIA3Automation())
            {
                Window window = app.GetMainWindow(auto);
                if (window == null)
                    return null;

                // A browser's name is for usual a postfix of its title
                int browserNameDelimiter = window.Title.LastIndexOf("-", StringComparison.InvariantCultureIgnoreCase);
                string hopefullyBrowserName;
                if (browserNameDelimiter <= 0 || browserNameDelimiter >= window.Title.Length - 2)
                    hopefullyBrowserName = window.Title;
                else
                    hopefullyBrowserName = window.Title.Substring(1 + browserNameDelimiter);

                hopefullyBrowserName = hopefullyBrowserName.ToLowerInvariant().Trim();


                AutomationElement res = null;

                #region SwitchOnBrowser

                if (hopefullyBrowserName.EndsWith("firefox"))
                    res = GetFirstMatchOrNull(window.FindAllDescendants(), Firefox);
                else if (hopefullyBrowserName.EndsWith("chrome")
                         || hopefullyBrowserName.EndsWith("brave")
                         || hopefullyBrowserName.EndsWith("edge"))
                    res = GetFirstMatchOrNull(window.FindAllDescendants(), ChromeBraveEdge);
                else if (hopefullyBrowserName.EndsWith("opera"))
                    res = GetFirstMatchOrNull(window.FindAllDescendants(), Opera);

                #endregion

                // Fallback-Case
                if (res == null)
                    // Ordered from most speficic to least speficic a few possible somewhat unique identifiers from some browsers. 
                    // We'll take the first and hope for the best
                    res = window                // Names as evaluated at 2026.09.10:
                          .FindAllDescendants() // Chromium
                          .Where(x => x.Properties.ControlType.ValueOrDefault == ControlType.Button)
                          .FirstOrDefault(x =>
                                              x.Properties.ClassName.ValueOrDefault ==
                                              "ReloadStopButton" // Opera
                                              || x.Properties.ClassName.ValueOrDefault ==
                                              "ReloadButton" // Chrome, Brave, Edge
                                              || x.Properties.AutomationId.ValueOrDefault ==
                                              "reload-button" // Firefox, loaded
                                              || x.Properties.AutomationId.ValueOrDefault ==
                                              "stop-button" // Firefox, loading
                                              || x.Properties.AutomationId.ValueOrDefault ==
                                              "view_1003"); // Currently [2026.09] the automationId in Chromium

                return res;
            }
        }


        private static AutomationElement GetFirstMatchOrNull
            (IEnumerable<AutomationElement> collection, BrowserLoadingButtons browser)
        {
            IEnumerable<AutomationElement> res = collection;

            if (browser.ClassName.Length > 0)
                res = res.Where(x => x.Properties.ClassName.ValueOrDefault == browser.ClassName);


            if (browser.AutomationIdLoaded.Length > 0 && browser.AutomationIdLoading.Length > 0)
                res = res.Where(x =>
                                    x.Properties.AutomationId.ValueOrDefault    == browser.AutomationIdLoaded
                                    || x.Properties.AutomationId.ValueOrDefault == browser.AutomationIdLoading);


            if (browser.ControlType != ControlType.Unknown)
                res = res.Where(x => x.Properties.ControlType.ValueOrDefault == browser.ControlType);


            return res.FirstOrDefault();
        }


        public override string ToString()
        {
            return
                $"{nameof(FrameworkId)}: {FrameworkId}, "                 +
                $"{nameof(ClassName)}: {ClassName}, "                     +
                $"{nameof(AutomationIdLoaded)}: {AutomationIdLoaded}, "   +
                $"{nameof(AutomationIdLoading)}: {AutomationIdLoading}, " +
                $"{nameof(ControlType)}: {ControlType}";
        }
    }
}