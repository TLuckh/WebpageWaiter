using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using KeePass.Util;
using NUnit.Framework;
using WebpageWaiter;

namespace WebpageWaiterTests
{
    [TestFixture]
    public class GeneralParsingTests
    {


        [SetUp]
        public void SetUp() { }

        [TearDown]
        public void TearDown() { }


        [Test]
        public void SequenceWithoutCSequence()
        {
            string sequence = "Hello World$!ß __ x3 ´´ !\"§%%&%$\"&";
            AutoTypeEventArgs e = new AutoTypeEventArgs(sequence,false,null,null);

            (List<(string partLeftOfSequence,CSequence cSequence)> parts,string final_part) res =
                WebpageWaiterExt._MethodParts.ExtractCSequences(e);
            
            Assert.True(res.parts.Count == 0);
        }
        
        
        [Test]
        public void SequenceWithOneIrrelevantCSequence()
        {
            string            sequence = "Hello World$!ß __ x3 ´{Sample}´ !\"§%%&%$\"&";
            AutoTypeEventArgs e        = new AutoTypeEventArgs(sequence,false,null,null);

            (List<(string partLeftOfSequence,CSequence cSequence)> parts,string final_part) res =
                WebpageWaiterExt._MethodParts.ExtractCSequences(e);
            
            Assert.True(res.parts.Count == 0);
        }
        
        [Test]
        public void IrrelevantAndRelevantCSequencesMixed()
        {
            string url = "https://google.com/query?=3";
            string relevantCSequence = "WebpageReady";
            string maxWaitTime = "100";

            string left = $"Hello World$!ß __ x3 ´{{Sample}}´ ";
            string middle = $"{{{relevantCSequence}:{maxWaitTime}}}";
            string right = $"!\"§%%&%$\"&";
            string sequence = left + middle + right;
            AutoTypeEventArgs e        = new AutoTypeEventArgs(sequence,false,null,null);

            (List<(string partLeftOfSequence,CSequence cSequence)> parts,string final_part) res =
                WebpageWaiterExt._MethodParts.ExtractCSequences(e);
            
            Assert.True(res.parts.Count == 1);
            Assert.True(res.parts[0].partLeftOfSequence == left);
            Assert.True(res.parts[0].cSequence is WaitForWebpageReady);
            var cSequence = (WaitForWebpageReady) res.parts[0].cSequence ;
            Assert.True(res.final_part== right);
        }
    }
}