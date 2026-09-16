using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using BrowserLoadingDetector;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Logging;

namespace WebpageWaiter;

/// <summary>
/// The {...}-sequences implemented in the plugin.
/// Extensions methods are supplied to get their 
/// </summary>
public enum CSequence
{
    /// <summary>
    /// 
    /// </summary>
    WaitForUrl,
    WaitForWebpageReady,
    SelectFirstPwEntry,
    SelectEditableEntryAbovePw,
    SelectEditableEntryBelowPw,
    SelectPwEntry,
    SelectFirstEditableEntry
}

public static class CSequencesExtensions
{
    private static readonly CSequence[] CSequenceValueArray = Enum.GetValues(typeof(CSequence)).Cast<CSequence>().ToArray();

    extension(CSequence)
    {
        /// <summary>
        /// Regex corresponding to a generic {...}-sequence as found in auto-type-sequences
        /// </summary>
        public static Regex GenericCSequence => new Regex(@"{[^{}]*}",RegexOptions.Compiled);
    }

    extension(CSequence source)
    {
        /// <summary>
        /// The underlying {...}-sequence of the enum value, encoded as Regex.
        /// 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If used on a non-existing CSequences-Value.</exception>
        public Regex Regex
        {
            get
            {
                return source switch // WARNING: Each Regex input string is a public API!
                {
                    CSequence.WaitForUrl          => new Regex(@"{WaitForUrl:([^{}]*)}",  RegexOptions.IgnoreCase),
                    CSequence.WaitForWebpageReady => new Regex(@"{WebpageReady:([^{}]*)}",RegexOptions.IgnoreCase),
                    CSequence.SelectFirstPwEntry  => new Regex(@"{SelectFirstPwEntry}",   RegexOptions.IgnoreCase),
                    CSequence.SelectEditableEntryAbovePw => new Regex(@"{SelectEditableEntryAbovePw:([^{}]*)}",
                                                                      RegexOptions.IgnoreCase),
                    CSequence.SelectEditableEntryBelowPw => new Regex(@"{SelectEditableEntryBelowPw:([^{}]*)}",
                                                                      RegexOptions.IgnoreCase),
                    CSequence.SelectPwEntry            => new Regex(@"{SelectPwEntry:([^{}]*)}",  RegexOptions.IgnoreCase),
                    CSequence.SelectFirstEditableEntry => new Regex(@"{SelectFirstEditableEntry}",RegexOptions.IgnoreCase),
                    _                                  => throw new ArgumentOutOfRangeException(nameof(source),source,null)
                };
            }
        }

        public string PlaceHolderString
        {
            get
            {
                return source switch // WARNING: Each Regex input string is a public API!
                {
                    CSequence.WaitForUrl                 => @"{WaitForUrl:URL}",
                    CSequence.WaitForWebpageReady        => @"{WebpageReady:WaitTimeMs}",
                    CSequence.SelectFirstPwEntry         => @"{SelectFirstPwEntry}",
                    CSequence.SelectEditableEntryAbovePw => @"{SelectEditableEntryAbovePw:INTEGER}",
                    CSequence.SelectEditableEntryBelowPw => @"{SelectEditableEntryBelowPw:INTEGER}",
                    CSequence.SelectPwEntry              => @"{SelectPwEntry:INTEGER}",
                    CSequence.SelectFirstEditableEntry   => @"{SelectFirstEditableEntry}",
                    _                                    => throw new ArgumentOutOfRangeException(nameof(source),source,null)
                };
            }
        }

        /// <summary>
        /// Checks whether <paramref name="input"/> matches one of the {...}-sequences of the enum (via enum.Regex).
        /// If so, it returns true and which enum value it matches, as well as the groups found.
        /// Otherwise, it returns false, and the value of the two out-args is undefined. 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="groups"></param>
        /// <returns></returns>
        public static bool TryParse(string input,out CSequence output,out GroupCollection groups)
        {
            foreach (CSequence cSequence in CSequenceValueArray)
            {
                Match match = cSequence.Regex.Match(input);
                if (!match.Success)
                    continue;

                output = cSequence;
                groups = match.Groups;
                return true;
            }

            output = default;
            groups = null;
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groups"></param>
        /// <returns></returns>
        /// <exception cref="InvalidCSequenceException"></exception>
        /// <exception cref="InvalidArgumentException">If one of the parameters given by the autotype sequence was wrong. See error message for info. Use .MessageBox, to show the user an error message window.</exception>
        public int GetMaxWaitTime(GroupCollection groups)
        {
            int maxWaitTime = source switch
            {
                CSequence.WaitForWebpageReady        => MaxWaitTimeMethods.WaitForWebpageReady(groups),
                CSequence.WaitForUrl                 => MaxWaitTimeMethods.WaitForUrl(groups),
                CSequence.SelectFirstPwEntry         => MaxWaitTimeMethods.SelectFirstPwEntry(groups),
                CSequence.SelectEditableEntryAbovePw => MaxWaitTimeMethods.SelectEditableEntryAbovePw(groups),
                CSequence.SelectEditableEntryBelowPw => MaxWaitTimeMethods.SelectEditableEntryBelowPw(groups),
                CSequence.SelectPwEntry              => MaxWaitTimeMethods.SelectPwEntry(groups),
                CSequence.SelectFirstEditableEntry   => MaxWaitTimeMethods.SelectFirstEditableEntry(groups),
                _ => throw new
                    InvalidCSequenceException($"The given CSequence {nameof(source)} with value {source} was of an unknown type.")
            };
            return maxWaitTime;
        }

        /// <summary>
        /// Performs the action corresponding to the CSequence instance (i.e. wait till the webpage is ready, wait till the passed url is shown, ...)
        /// into the currently focussed window.
        /// <br/>
        /// Currently, if the active window changes, it stops inputting, but continues if within the maxWaitTime, the window comes back into focus.
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="maxWaitTime">Timeout time in ms</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public bool Perform(GroupCollection groups,int maxWaitTime,int retryDelayMs = 100,int initialDelayMs = 100)
        {
            IntPtr foregroundWindow = MarshallingMethods.GetForegroundWindow();
            MarshallingMethods.GetWindowThreadProcessId(foregroundWindow,out uint foregroundWindowProcessId);


            var startTime = DateTime.Now;
            var endTime   = startTime.AddMilliseconds(maxWaitTime);

            var firstLoop = true;
            while (DateTime.Now < endTime)
            {
                Thread.Sleep(firstLoop ? initialDelayMs : retryDelayMs);
                firstLoop = false;

                if (MarshallingMethods.GetForegroundWindow() != foregroundWindow) continue;

                try
                {
                    bool success = source switch
                    {
                        CSequence.WaitForWebpageReady => PerformMethods.WaitForWebpageReady(groups,foregroundWindowProcessId),
                        CSequence.WaitForUrl          => PerformMethods.WaitForUrl(groups,foregroundWindowProcessId),
                        CSequence.SelectFirstPwEntry  => PerformMethods.SelectFirstPwEntry(groups,foregroundWindowProcessId),
                        CSequence.SelectEditableEntryAbovePw =>
                            PerformMethods.SelectEditableEntryAbovePw(groups,foregroundWindowProcessId),
                        CSequence.SelectEditableEntryBelowPw =>
                            PerformMethods.SelectEditableEntryBelowPw(groups,foregroundWindowProcessId),
                        CSequence.SelectPwEntry => PerformMethods.SelectPwEntry(groups,foregroundWindowProcessId),
                        CSequence.SelectFirstEditableEntry =>
                            PerformMethods.SelectFirstEditableEntry(groups,foregroundWindowProcessId),
                        _ => throw new
                            InvalidCSequenceException($"The given CSequence {nameof(source)} with value {source} was of an unknown type.")
                    };
                    if (success) return success;
                }
                catch (InvalidCSequenceException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    // ToDo: Logging
                }
            }

            return false;
        }
    }

    public static class MaxWaitTimeMethods
    {
        /// <summary>
        /// Returns true if webpage has finished loading, and false if not (also returns false if an error happened while trying to find it out).
        /// Retry-Logic and error handling has to be done outside. 
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="foregroundWindow"></param>
        /// <returns></returns>
        public static int WaitForWebpageReady(GroupCollection groups)
        {
            // Validate input:
            if (groups.Count < 1)
            {
                throw new InvalidArgumentException("Webpage-Waiter Plugin Autotype-Sequence Error",
                                                   "In the custom sequence {WebpageReady:WaitTimeMs}, WaitTimeMs has not been set"
                                                   
                                                  );
            }

            if (!int.TryParse(groups[1].Value,out int maxWaitTime))
            {
                throw new InvalidArgumentException("Webpage-Waiter Plugin Autotype-Sequence Error",
                                                   "In the custom sequence {WebpageReady:WaitTimeMs}, WaitTimeMS must be a positive integer"
                                                   
                                                  );
                
            }


            return maxWaitTime;
        }


        /// <summary>
        /// ToDo: Untested. Got it from an old snippet.
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="foregroundWindow"></param>
        /// <returns></returns>
        public static int WaitForUrl(GroupCollection groups)
        {
            throw new NotImplementedException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static int SelectFirstPwEntry(GroupCollection groups)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static int SelectEditableEntryAbovePw(GroupCollection groups)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static int SelectEditableEntryBelowPw(GroupCollection groups)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static int SelectPwEntry(GroupCollection groups)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static int SelectFirstEditableEntry(GroupCollection groups)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all
    }

    public static class PerformMethods
    {
        /// <summary>
        /// Returns true if webpage has finished loading, and false if not (also returns false if an error happened while trying to find it out).
        /// Retry-Logic and error handling has to be done outside. 
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="foregroundWindow"></param>
        /// <returns></returns>
        public static bool WaitForWebpageReady(GroupCollection groups,uint foregroundWindow)
        {
            // Originally via DataStructures.WebpageReady(), but using FlaUI was easier in hindsight, since the busy state wasn't reliable


            AutomationElement button = BrowserLoadingButtons.GetLoadingButton(delayMs: 0,processId: foregroundWindow);

            if (button == null)
            {
                Debug.WriteLine("Couldn't get button");
                return false;
            }

            BitmapMat buttonState = Program.GetLoadingButtonBitmap(button);
            if (buttonState == null)
            {
                Debug.WriteLine("Couldn't get button bitmap.");
                return false;
            }

            bool isLoadingFinished = Program.HasLoadingFinished(buttonState);
            return isLoadingFinished;
        }


        /// <summary>
        /// ToDo: Untested. Got it from an old snippet.
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="foregroundWindow"></param>
        /// <returns></returns>
        public static bool WaitForUrl(GroupCollection groups,uint foregroundWindow)
        {
            var currentUrl = DataStructures.GetUrl();
            return string.Equals(currentUrl,groups[1].ToString(),StringComparison.InvariantCultureIgnoreCase);
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static bool SelectFirstPwEntry(GroupCollection groups,uint foregroundWindow)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static bool SelectEditableEntryAbovePw(GroupCollection groups,uint foregroundWindow)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static bool SelectEditableEntryBelowPw(GroupCollection groups,uint foregroundWindow)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static bool SelectPwEntry(GroupCollection groups,uint foregroundWindow)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all

        public static bool SelectFirstEditableEntry(GroupCollection groups,uint foregroundWindow)
        {
            throw new InvalidOperationException();
        } //ToDo Use WebAutoType for this, they alrady did the work after all
    }
}

public class InvalidArgumentException : Exception
{
    public string                 Title   { get; }
    public override string Message { get; }


    public InvalidArgumentException(string title,string message)
    {
        Title   = title;
        Message = message;
    }

    public void ShowErrorMessage()
    {
        MessageBox.Show(
                        Message,
                        Title,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                       );
    
        // Better use what is used internally for error messages?:
        // throw new FormatException(exception.Title        +
        //                           MessageService.NewLine +
        //                           exception.Message);
        // return;
        
    }
}

public class InvalidCSequenceException : Exception
{
    public InvalidCSequenceException() { }

    public InvalidCSequenceException(string message) : base(message) { }

    public InvalidCSequenceException(string message,Exception innerException) : base(message,innerException) { }

    protected InvalidCSequenceException(SerializationInfo info,StreamingContext context) : base(info,context) { }
}