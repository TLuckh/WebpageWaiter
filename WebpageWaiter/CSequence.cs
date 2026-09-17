using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using BrowserLoadingDetector;
using FlaUI.Core.AutomationElements;

namespace WebpageWaiter;

/// <summary>
/// Base class for all custom {...}-sequences implemented in the plugin.
/// </summary>
public abstract class CSequence // I'd love this to be an interface with abstract static members... but alas net48...
{
    /// <summary>
    /// Regex corresponding to a generic {...}-sequence as found in auto-type-sequences.
    /// </summary>
    public static Regex GenericCSequence { get; } =
        new Regex(@"{[^{}]*}",RegexOptions.Compiled);

    /// <summary>
    /// The maximum amount of time in milliseconds for which <see cref="Perform"/>
    /// retries the operation.
    /// </summary>
    public int MaxWaitTime { get; protected set; }

    /// <summary>
    /// The placeholder string of this sequence.
    /// </summary>
    public abstract string PlaceHolderString { get; }

    protected CSequence(GroupCollection groups)
    {
        if (groups == null)
            throw new ArgumentNullException(nameof(groups));
    }

    /// <param name="input">The input that should be checked. Should contain at most one valid {...}-sequence (split it before using GenericCSequence).</param>
    /// <summary>
    ///     Checks whether <paramref name="input"/> matches one of the {...}-sequences
    ///     of the plugin.
    /// 
    ///     If so, it returns true and a new instance of the matching sequence object, which validates the inputs during construction.
    ///     Otherwise, it returns false, and the value of the two out-args is undefined.
    /// 
    ///     The input should contain at most one valid {...}-sequence
    /// 
    ///     This switch must be extended manually whenever a new CSequence subtype
    ///     is added.
    /// </summary>
    /// <param name="output">The matching custom sequence.</param>
    /// <param name="groups">The groups found by the matching regular expression.</param>
    /// <returns><c>true</c> if the input matches a known custom sequence; otherwise <c>false</c>.</returns>
    public static bool TryParse(string input,out CSequence output)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        switch (input)
        {
            case string value when WaitForWebpageReady.TryCreate(value,out WaitForWebpageReady webpageReady):
                output = webpageReady;
                return true;


            default:
                output = null;
                return false;
        }
    }

    /// <summary>
    /// Validates the parameters supplied by the auto-type sequence.
    /// </summary>
    /// <param name="groups">The groups found by the sequence's regular expression.</param>
    /// <exception cref="InvalidCSequenceException">If the sequence type is invalid.</exception>
    /// <exception cref="InvalidArgumentException">If one of the parameters was wrong.</exception>
    public abstract void Validate(GroupCollection groups);

    /// <summary>
    /// Returns the maximum wait time stored during construction and validation.
    /// </summary>
    public int GetMaxWaitTime() { return MaxWaitTime; }

    /// <summary>
    /// Performs the action corresponding to this custom sequence
    /// (i.e. wait till the webpage is ready, wait till the passed URL is shown, ...)
    /// into the currently focussed window.
    ///
    /// Currently, if the active window changes, it stops inputting, but continues
    /// if within the maxWaitTime, the window comes back into focus.
    /// </summary>
    /// <param name="retryDelayMs">Timeout between retries in ms.</param>
    /// <param name="initialDelayMs">Initial delay in ms.</param>
    /// <returns><c>true</c> if the action succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="InvalidCSequenceException">.</exception>
    public bool Perform(int retryDelayMs = 100,int initialDelayMs = 100)
    {
        if (retryDelayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(retryDelayMs));

        if (initialDelayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(initialDelayMs));

        IntPtr foregroundWindow = MarshallingMethods.GetForegroundWindow();
        MarshallingMethods.GetWindowThreadProcessId(foregroundWindow,out uint foregroundWindowProcessId);

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool      firstLoop = true;

        while (stopwatch.ElapsedMilliseconds < MaxWaitTime)
        {
            Thread.Sleep(firstLoop ? initialDelayMs : retryDelayMs);
            firstLoop = false;

            if (MarshallingMethods.GetForegroundWindow() != foregroundWindow)
                continue;

            try
            {
                if (TryPerform(foregroundWindowProcessId))
                    return true;
            }
            catch (InvalidCSequenceException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // ToDo: Logging
                Debug.WriteLine($"Error while performing {GetType().Name}: {exception}");
            }
        }

        return false;
    }

    /// <summary>
    /// Performs one attempt of the operation corresponding to the concrete sequence.
    /// Retry logic and error handling are implemented by <see cref="Perform"/>.
    /// </summary>
    /// <param name="foregroundWindowProcessId">The process ID of the relevant foreground window.</param>
    /// <returns><c>true</c> if the operation succeeded; otherwise <c>false</c>.</returns>
    protected abstract bool TryPerform(uint foregroundWindowProcessId);

    /// <summary>
    /// Reads a positive integer from a named regular-expression group.
    /// </summary>
    /// <param name="groups">The groups found by the sequence's regular expression.</param>
    /// <param name="groupName">The name of the group containing the integer.</param>
    /// <param name="sequenceName">The name of the sequence used in the error message.</param>
    /// <param name="parameterName">The name of the parameter used in the error message.</param>
    /// <returns>The parsed positive integer.</returns>
    /// <exception cref="InvalidArgumentException">If the group is missing or does not contain a positive integer.</exception>
    protected static int GetRequiredPositiveInt(GroupCollection groups,string groupName,string sequenceName,string parameterName)
    {
        if (groups == null || !groups[groupName].Success)
        {
            throw new InvalidArgumentException(
                                               "Webpage-Waiter Plugin Autotype-Sequence Error",
                                               $"In the custom sequence {sequenceName}, {parameterName} has not been set.");
        }

        if (!int.TryParse(groups[groupName].Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int result) || result <= 0)
        {
            throw new InvalidArgumentException(
                                               "Webpage-Waiter Plugin Autotype-Sequence Error",
                                               $"In the custom sequence {sequenceName}, {parameterName} must be a positive integer.");
        }

        return result;
    }

    /// <summary>
    /// Reads a non-negative integer from a named regular-expression group.
    /// </summary>
    /// <param name="groups">The groups found by the sequence's regular expression.</param>
    /// <param name="groupName">The name of the group containing the integer.</param>
    /// <param name="sequenceName">The name of the sequence used in the error message.</param>
    /// <param name="parameterName">The name of the parameter used in the error message.</param>
    /// <returns>The parsed non-negative integer.</returns>
    /// <exception cref="InvalidArgumentException">If the group is missing or does not contain a non-negative integer.</exception>
    protected static int GetRequiredNonNegativeInt(GroupCollection groups,string groupName,string sequenceName,string parameterName)
    {
        if (groups == null || !groups[groupName].Success)
        {
            throw new InvalidArgumentException(
                                               "Webpage-Waiter Plugin Autotype-Sequence Error",
                                               $"In the custom sequence {sequenceName}, {parameterName} has not been set.");
        }

        if (!int.TryParse(groups[groupName].Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int result) || result < 0)
        {
            throw new InvalidArgumentException(
                                               "Webpage-Waiter Plugin Autotype-Sequence Error",
                                               $"In the custom sequence {sequenceName}, {parameterName} must be a non-negative integer.");
        }

        return result;
    }
}

/// <summary>
/// Waits until the webpage has finished loading.
/// </summary>
public sealed class WaitForWebpageReady : CSequence
{
    private BitmapMat _buttonState;

    public static Regex Regex { get; } = new Regex(
                                                   $@"\{{WebpageReady:(?<{nameof(MaxWaitTime)}>[^{{}}]*)\}}",
                                                   RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public const string _PlaceHolderString = @"{WebpageReady:WaitTimeMs}";

    public override string PlaceHolderString => _PlaceHolderString;

    private WaitForWebpageReady(GroupCollection groups) : base(groups) { Validate(groups); }

    public static bool TryCreate(string input,out WaitForWebpageReady output)
    {
        Match match = Regex.Match(input);

        if (!match.Success)
        {
            output = null;
            return false;
        }

        output = new WaitForWebpageReady(match.Groups);
        return true;
    }

    /// <summary>
    /// Validates WaitTimeMs and stores it as maxWaitTime.
    /// </summary>
    /// <param name="groups">The groups found by the sequence's regular expression.</param>
    public override void Validate(GroupCollection groups)
    {
        MaxWaitTime = GetRequiredPositiveInt(groups,nameof(MaxWaitTime),PlaceHolderString,"WaitTimeMs");
    }

    /// <summary>
    /// Returns true if webpage has finished loading, and false if not.
    /// It also returns false if an error happened while trying to find it out.
    /// Retry logic and error handling are implemented by <see cref="CSequence.Perform"/>.
    /// </summary>
    /// <param name="foregroundWindowProcessId">The process ID of the browser window.</param>
    /// <returns><c>true</c> if webpage loading has finished; otherwise <c>false</c>.</returns>
    protected override bool TryPerform(uint foregroundWindowProcessId)
    {
        // Originally via DataStructures.WebpageReady(), but using FlaUI was easier in hindsight,
        // since the busy state wasn't reliable.

        AutomationElement button = BrowserLoadingButtons.GetLoadingButton(
                                                                          delayMs: 0,
                                                                          processId: foregroundWindowProcessId);

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

        // Caching works out temporally: I
        // In the first run the previously cached state is null and so a change occurs.
        // Afterwards, if the state doesn't change, then we're still loading.
        if (buttonState.Equals(_buttonState))
            return false;
        else
            _buttonState = buttonState;


        bool isLoadingFinished = Program.HasLoadingFinished(buttonState);
        return isLoadingFinished;
    }
}

public class InvalidArgumentException : Exception
{
    public          string Title   { get; }
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
                        MessageBoxIcon.Error);
    }
}

public class InvalidCSequenceException : Exception
{
    public InvalidCSequenceException() { }

    public InvalidCSequenceException(string message) : base(message) { }

    public InvalidCSequenceException(string message,Exception innerException) : base(message,innerException) { }

    protected InvalidCSequenceException(SerializationInfo info,StreamingContext context) : base(info,context) { }
}