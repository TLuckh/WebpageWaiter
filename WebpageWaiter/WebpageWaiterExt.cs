using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FlaUI.Core;
using KeePass.Plugins;
using KeePass.Resources;
using KeePass.Util;
using KeePass.Util.Spr;
using KeePassLib.Utility;
using Debug = System.Diagnostics.Debug;

// ToDo: Make HoughCircle only check center-points of the circle near the center of the picture? Not sure tbh
// ToDo: Add in .csproj that the Release-Build builds against the KeePass.exe (do I need to sign my assembly)?
// ToDo: Ship as Plugin-Extension-Format?


namespace WebpageWaiter
{
    public sealed class WebpageWaiterExt : Plugin
    {
        private IPluginHost m_host = null;

        private List<string> _placeHolderHints =
        [WaitForUrl._PlaceHolderString,
            WaitForWebpageReady._PlaceHolderString,
            SelectFirstPwEntry._PlaceHolderString,
            SelectEditableEntryAbovePw._PlaceHolderString,
            SelectEditableEntryBelowPw._PlaceHolderString,
            SelectPwEntry._PlaceHolderString,
            SelectFirstEditableEntry._PlaceHolderString
        ];

        public override bool Initialize(IPluginHost host)
        {
            if (host == null) return false;
            m_host                                 =  host;
            KeePass.Util.AutoType.FilterCompilePre += HandleAutoTypeFilterCompilePre;

            _placeHolderHints.ForEach(placeHolderString => SprEngine.FilterPlaceholderHints.Add(placeHolderString));
            return true;
        }
        public override void Terminate()
        {
            KeePass.Util.AutoType.FilterCompilePre -= HandleAutoTypeFilterCompilePre;
            _placeHolderHints.ForEach(placeHolderString => SprEngine.FilterPlaceholderHints.Remove(placeHolderString));

        }

        public static void HandleAutoTypeFilterCompilePre(object sender,AutoTypeEventArgs e)
        {
            (List<(string partLeftOfSequence, CSequence cSequence)> parts, string finalPart) = _MethodParts.ExtractCSequences(e);


            // Our plugin doesn't have anything to do if there's none of our plugin-specific {...}-sequences in the auto-type sequence
            // And if it has to take over, it consumes the sequence and only passes onward slected parts
            if (parts.Count == 0)
                return;
            
            e.Sequence = ""; // From here on, we can just return if anything goes wrong, and the autotyping will be stopped.

            try
            {
                foreach (var (irrelevantAutotypeSequence,cSequence) in parts)
                {
                    // Perform the part of the auto-type-sequence preceeding the plugin-specific {...}-sequence
                    KeePass.Util.AutoType.PerformIntoCurrentWindow(e.Entry,e.Database,irrelevantAutotypeSequence);
                    // Wait a little so we don't query before the webpage even started loading
                    Thread.Sleep(100);

                    //ToDo:Get browser state & active page per IAccessible (per Chrome Remote Debug Protocol?)
                    // List<PwFieldAndSurroundingFields> fields = DataStructures.GetCredentialFields();
                    // ToDo: switch over cSequence and do what has to be done:
                    int  maxWaitTime = cSequence.GetMaxWaitTime();
                    bool success     = cSequence.Perform();

                    if (!success)
                        return; // Something went wrong.
                }
                
                KeePass.Util.AutoType.PerformIntoCurrentWindow(e.Entry,e.Database,finalPart);
            }
            catch (InvalidArgumentException exception)
            {
                exception.ShowErrorMessage();

            }
            catch (InvalidCSequenceException exception)
            {
                Debug.WriteLine(exception);
                return;
            }
            
        }


        internal static class _MethodParts
        {
            /// <summary>
            /// For a given AutoTypeEvent e, its autotype-sequence can be partitioned into repeated parts of the shape
            /// "&lt;irrelevant_string&gt; &lt;{...}-sequence&gt;", with possibly one trailing &lt;irrelevant_string&gt;.
            ///
            /// <br></br>
            /// Here, each &lt;irrelevant_string&gt;-instance is a string without any curly bracket which would need to be parsed
            /// (more specifically, curly brackets of the shape given by CSequence.GenericCSequence).
            /// 
            /// We partition the string as shown, and output for each such partition-part a tuple (irrelevant_string, matched_cSequence, parts_of_matched_cSequence).
            /// </summary>
            /// <param name="e">The AutoTypeEvent to process.</param>
            /// <returns>
            /// A sequence of tuples, where:
            /// <list type="bullet">
            ///   <item>
            ///     <description>The left element is an auto-type sequence which contains nothing our plugin has to act on.</description>
            ///   </item>
            ///   <item>
            ///     <description>The middle element is a subtype of CSequence instance, encoding which plugin-{...}-sequence we matched and what parameters it's got.</description>
            ///   </item>
            /// </list>
            /// </returns>

            public static (List<(string partLeftOfSequence, CSequence cSequence)> parts, string finalPart) ExtractCSequences(AutoTypeEventArgs e)
            {
                string sequence = e.Sequence;

                  
                List<(string partLeftOfSequence,CSequence cSequence)> parts      = [];
                string                                                finalPart = "";
                // The sequence is of the form "<irrelevant_string> <plugin-sequence> ...",
                // where <irrelevant_string> is a maximal part that doesn't contain any squirly brackets with contents that concern our plugin,
                // and  <{...}-sequences> is the following part, which *is* a plugin-relevant sequence.
                // We split this sequence into (<irrelevant_string> ,<instance for {...}-sequence>) tuples, which we put into parts.
                // The sequence may end on a <irrelevant_string>, which we in that case put into finalPart.
                
                int matchSearchStart = 0;
                while (CSequence.GenericCSequence.Match(sequence,matchSearchStart) is Match { Success: true } match)
                {
                    matchSearchStart =match.Index + match.Length;
                    
                    if (CSequence.TryParse(match.Value,out CSequence cSequence))                            // If a user input an invalid CSequence, then we effectively pass it back to KeePass, where it is detected as such
                    {
                        string partLeftOfSequence  = sequence.Substring(0,match.Index);
                        string partRightOfSequence = sequence.Substring(match.Index + match.Length);
                        parts.Add((partLeftOfSequence,cSequence));
                        
                        sequence = partRightOfSequence;
                        matchSearchStart = 0;
                    }
                }

                if (sequence != "")
                    finalPart = sequence;


                return (parts, finalPart);
            }

        }



    }


    internal static class TargetPatterns
    {
        /// <summary>
        /// Regex corresponding to a generic {...}-sequence as found in auto-type-sequences
        /// </summary>
        internal static Regex specialSequences = new Regex(@"{[^{}]*}",RegexOptions.Compiled);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input">A {...}-sequence</param>
        /// <returns>Whether the pattern is "{WEBPAGEREADY}" </returns>
        internal static bool IsPattern_WaitForWebpageReady(string input)
        {
            const string patternStart = @"{WEBPAGEREADY}";
            return input.Equals(patternStart,StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input">A {...}-sequence</param>
        /// <param name="urlName">The &lt;URL&gt;-part of the {...}-sequence, if and only if the method returns True.</param>
        /// <returns>Whether the pattern is "{WAITFORURL:&amp;lt;URL&amp;gt;}" </returns>
        internal static bool IsPattern_WaitForUrlName(string input,out string urlName)
        {
            urlName = "";
            const string patternStart = @"{WAITFORURL:";

            if (!input.StartsWith(patternStart,StringComparison.InvariantCultureIgnoreCase)) return false;

            urlName = input.Remove(0,patternStart.Length);
            return true;
        }
    }
}