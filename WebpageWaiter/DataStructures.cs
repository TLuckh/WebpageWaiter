using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Accessibility;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using WebpageWaiter.WebAutoType;
using Application = FlaUI.Core.Application;

namespace WebpageWaiter;




public static class DataStructures
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(
        IntPtr   hWnd,
        out uint lpdwProcessId);
    
    /// <summary>
    /// Returns the URL of the currently focussed window, if that window is a browser.
    /// </summary>
    /// <returns></returns>
    public static bool WebpageReady()
    {
        IntPtr hWnd = GetForegroundWindow();
        DataStructures.GetWindowThreadProcessId(hWnd, out uint processId);

        Application app = FlaUI.Core.Application.Attach((int)processId);

        using (var automation = new UIA3Automation())
        {
            Window? window = app.GetMainWindow(automation);
            if (window == null) return false;

            // Alle Descendants zählen
            AutomationElement w = window;

        }

        IAccessible?    doc  = BrowserUrlReader.Create(hWnd)?.GetDocument();
        return doc != null && ((AccessibleStates) doc.accState[0]).HasFlag(AccessibleStates.Busy);
    }
    /// <summary>
    /// Returns the URL of the currently focussed window, if that window is a browser.
    /// </summary>
    /// <returns></returns>
    public static string GetUrl()
    {
        IntPtr hWnd = GetForegroundWindow();
        var    url  = BrowserUrlReader.Create(hWnd)?.GetBrowserFocusUrl(out bool passwordFieldFocussed);
        return url ?? string.Empty;
    }

    /// <summary>
    /// If the browser is currently in focus, returns for the current tab:
    /// (url,title,selectedText)
    /// Each of which might be ""
    /// </summary>
    /// <returns></returns>
    public static (string,string,string) Get_Url_Title_SelectedText()
    {
        IntPtr hWnd = GetForegroundWindow();
        string title = "";
        string selectedText = "";
        string url = BrowserUrlReader.Create(hWnd)?.GetBrowserFocusUrlWithInfo(out title,out selectedText) 
                     ?? "";
        
        return (url,title,selectedText);

    }
        

    /// <summary>
    /// Gibt (FeldVorPW, PW-Feld, FeldNachPW) zurück.
    /// Felder die nicht gefunden wurden, sind null.
    /// </summary>
    public static List<PwFieldAndSurroundingFields> GetCredentialFields()
    {
        List<PwFieldAndSurroundingFields> results = [];

        IntPtr hWnd = GetForegroundWindow();
        var doc = BrowserUrlReader.Create(hWnd)?.GetDocument();
        if (doc == null)
            return (results);

        // Sammle editierbare und sichtbare Felder per BFS gemäß der Hierarchie der Webseite.
        var allFields = CollectEditableFields(doc);

        // Alle PW-Felder sowie das jeweils unmittelbar vor- und nachfolgende editierbare Textfeld finden
        // und zu results hinzufügen.
        if (allFields.Count == 0) return results;

        for (int index=0; index< allFields.Count; index++)
        {
            AccessibleField currentField = allFields[index];
            
            if (!currentField.IsPassword) continue;
            
            results.Add(new PwFieldAndSurroundingFields(
                fieldBefore: index>0 ? allFields[index-1] : null,
                passwordField: currentField,
                fieldAfter: index < allFields.Count - 1 ? allFields[index + 1] : null
            ));

        }

        return results;
    }

// ── Alle editierbaren Felder rekursiv sammeln ────────────────────
    private static List<AccessibleField> CollectEditableFields(IAccessible parent)
    {
        List<AccessibleField> results = [];
        
        // BFS over all fields
        Queue<IAccessible> queue = [];
        queue.Enqueue(parent);
        while (queue.Count > 0)
        {
            IAccessible currentNode = queue.Dequeue();
            
            // BFS-part: Add children to queue
            foreach (IAccessible node in AccessibleObjectHelper.GetChildren(currentNode))
            {
                queue.Enqueue(node);
            }

            AddIfEditableText(currentNode, results);
        }
        return results;

        // Adds currentNode as AccessibleField to results if it is a visible & editable password field
        void AddIfEditableText(IAccessible currentNode, List<AccessibleField> results)
        {
            try
            {
                var role = (AccessibleRole)(int)currentNode.accRole[0];

                if (role != AccessibleRole.Text) return;
                
                bool isReadOnly  = AccessibleObjectHelper.HasState(currentNode, AccessibleStates.ReadOnly); 
                bool isProtected = AccessibleObjectHelper.HasState(currentNode, AccessibleStates.Protected);
                bool isFocused   = AccessibleObjectHelper.HasState(currentNode, AccessibleStates.Focused);
                bool isInvisible = AccessibleObjectHelper.HasState(currentNode, AccessibleStates.Invisible);

                // Nur editierbare, sichtbare Felder
                if (isReadOnly || isInvisible) return;
                
                results.Add(new AccessibleField(
                
                    element : currentNode,
                    value : AccessibleObjectHelper.SafeGetValue(currentNode) ?? "<Unknown>",
                    isPassword : isProtected,
                    isFocused : isFocused
                ));

            }
            catch (Exception)
            {
            }
        }
    }
}

/// <summary>
/// Combines a password field together with the previous and next field that is editable.
/// 
/// </summary>
/// <param name="fieldBefore"></param>
/// <param name="passwordField"></param>
/// <param name="fieldAfter"></param>
public class PwFieldAndSurroundingFields(AccessibleField? fieldBefore,AccessibleField passwordField,AccessibleField? fieldAfter) {}

// Hilfsklasse für ein gefundenes Feld
public class AccessibleField(IAccessible element, string value, bool isPassword, bool isFocused)
{
    public IAccessible Element { get; } = element;
    public string Value { get; } = value;
    public bool IsPassword { get; } = isPassword;
    public bool IsFocused { get; } = isFocused;

    /// <summary>
    /// Sets the element currently in focus to <see cref="Element"/>.
    /// </summary>
    /// <returns></returns>
    public bool SetFocussed()
    {
        try
        {
            Element.accSelect(SELFLAG_TAKEFOCUS);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }

    }
    private const int SELFLAG_TAKEFOCUS = 1;    // From https://learn.microsoft.com/en-us/windows/win32/winauto/selflag
}

public class SelectionException : Exception
{
    public SelectionException() {
    }

    public SelectionException(string message) : base(message)
    {
    }

    public SelectionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected SelectionException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}