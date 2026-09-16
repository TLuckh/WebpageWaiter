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

// ToDo: Resize image before doing Hough. Even on Full HD it's pretty work intensive; We can also only do it once, save the HelpString (though I had browsers not give any...), and then wait for it to change
// Houghs is still more agnostic than looking for a change of loading button help text, or change of URL, so I probably shouldn't completely remove it
// ToDo: Turn the CSeqeuence-Enum into a (closed) subtype hierarchy (validation needs type specific fields to save good results in)
// ToDo: Make HoughCircle only check center-points of the circle near the center of the picture? Not sure tbh
// ToDo: Add in .csproj that the Release-Build builds against the KeePass.exe (do I need to sign my assembly)?
// ToDo: Ship as Plugin-Extension-Format?


namespace WebpageWaiter
{
    public sealed class WebpageWaiterExt : Plugin
    {
        private IPluginHost m_host = null;

        public override bool Initialize(IPluginHost host)
        {
            if (host == null) return false;
            m_host                                 =  host;
            KeePass.Util.AutoType.FilterCompilePre += HandleAutoTypeFilterCompilePre;

            Enum.GetValues(typeof(CSequence))
                .Cast<CSequence>()
                .Select(cSequence => cSequence.PlaceHolderString)
                .ToList()
                .ForEach(placeHolderString => SprEngine.FilterPlaceholderHints.Add(placeHolderString));
            return true;
        }
        public override void Terminate()
        {
            KeePass.Util.AutoType.FilterCompilePre -= HandleAutoTypeFilterCompilePre;
            Enum.GetValues(typeof(CSequence))
                .Cast<CSequence>()
                .Select(cSequence => cSequence.PlaceHolderString)
                .ToList()
                .ForEach(placeHolderString => SprEngine.FilterPlaceholderHints.Remove(placeHolderString));
        }

        public static void HandleAutoTypeFilterCompilePre(object sender,AutoTypeEventArgs e)
        {
            (List<(string partLeftOfSequence, CSequence cSequence, GroupCollection groups)> parts, string finalPart) = _MethodParts.ExtractCSequences(e);


            // Our plugin doesn't have anything to do if there's none of our plugin-specific {...}-sequences in the auto-type sequence
            // And if it has to take over, it consumes the sequence and only passes onward slected parts
            if (parts.Count == 0)
                return;
            
            e.Sequence = ""; // From here on, we can just return if anything goes wrong, and the autotyping will be stopped.

            try
            {
                foreach (var (irrelevantAutotypeSequence,cSequence,groups) in parts)
                {
                    // Perform the part of the auto-type-sequence preceeding the plugin-specific {...}-sequence
                    KeePass.Util.AutoType.PerformIntoCurrentWindow(e.Entry,e.Database,irrelevantAutotypeSequence);
                    // Wait a little so we don't query before the webpage even started loading
                    Thread.Sleep(250);

                    //ToDo:Get browser state & active page per IAccessible (per Chrome Remote Debug Protocol?)
                    // List<PwFieldAndSurroundingFields> fields = DataStructures.GetCredentialFields();
                    // ToDo: switch over cSequence and do what has to be done:
                    int  maxWaitTime = cSequence.GetMaxWaitTime(groups);
                    bool success     = cSequence.Perform(groups,maxWaitTime);

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
            ///     <description>The middle element is an enum value encoding which plugin-{...}-sequence we matched.</description>
            ///   </item>
            ///   <item>
            ///     <description>The right element is the groups of the enum_value.Regex Match we got.</description>
            ///   </item>
            /// </list>
            /// </returns>

            public static (List<(string partLeftOfSequence, CSequence cSequence, GroupCollection groups)> parts,string final_part) ExtractCSequences(AutoTypeEventArgs e)
            {
                string sequence = e.Sequence;

                  
                List<(string partLeftOfSequence,CSequence cSequence,GroupCollection groups)> parts      = [];
                string                                                                       final_part = "";
                // The sequence is of the form "<irrelevant_string> <plugin-sequence> ...",
                // where <irrelevant_string> is a maximal part that doesn't contain any squirly brackets with contents that concern our plugin,
                // and  <{...}-sequences> is the following part, which *is* a plugin-relevant sequence.
                // We split this sequence into (<irrelevant_string> ,<enum for {...}-sequence>, <groups of the {...}-sequence) tuples, which we put into parts.
                // The sequence may end on a <irrelevant_string>, which we in that case put into final_part.
                
                int matchSearchStart = 0;
                while (CSequence.GenericCSequence.Match(sequence,matchSearchStart) is Match { Success: true } match)
                {
                    matchSearchStart =match.Index + match.Length;
                    
                    if (CSequence.TryParse(match.Value,out CSequence cSequence,out GroupCollection? groups))
                    {
                        string partLeftOfSequence  = sequence.Substring(0,match.Index);
                        string partRightOfSequence = sequence.Substring(match.Index + match.Length);
                        parts.Add((partLeftOfSequence,cSequence,groups));
                        
                        sequence = partRightOfSequence;
                        matchSearchStart = 0;
                    }
                }

                if (sequence != "")
                    final_part = sequence;


                return (parts, final_part);
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