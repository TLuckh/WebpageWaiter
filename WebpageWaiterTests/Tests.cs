using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using KeePass.Util;
using NUnit.Framework;
using WebpageWaiter;

namespace WebpageWaiterTests
{
    [TestFixture]
    public class Tests
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

            (List<(string partLeftOfSequence,CSequence cSequence,GroupCollection groups)> parts,string final_part) res =
                WebpageWaiterExt._MethodParts.ExtractCSequences(e);
            
            Assert.True(res.parts.Count == 0);
        }
        
        
        [Test]
        public void SequenceWithOneIrrelevantCSequence()
        {
            string            sequence = "Hello World$!ß __ x3 ´{Sample}´ !\"§%%&%$\"&";
            AutoTypeEventArgs e        = new AutoTypeEventArgs(sequence,false,null,null);

            (List<(string partLeftOfSequence,CSequence cSequence,GroupCollection groups)> parts,string final_part) res =
                WebpageWaiterExt._MethodParts.ExtractCSequences(e);
            
            Assert.True(res.parts.Count == 0);
        }
        
        [Test]
        public void IrrelevantAndRelevantCSequencesMixed()
        {
            CSequence cSequence = CSequence.WaitForUrl;
            string url = "https://google.com/query?=3";
            string relevantCSequence = nameof(CSequence.WaitForUrl);

            string left = $"Hello World$!ß __ x3 ´{{Sample}}´ ";
            string middle = $"{{{relevantCSequence}:{url}}}";
            string right = $"!\"§%%&%$\"&";
            string sequence = left + middle + right;
            AutoTypeEventArgs e        = new AutoTypeEventArgs(sequence,false,null,null);

            (List<(string partLeftOfSequence,CSequence cSequence,GroupCollection groups)> parts,string final_part) res =
                WebpageWaiterExt._MethodParts.ExtractCSequences(e);
            
            Assert.True(res.parts.Count == 1);
            Assert.True(res.parts[0].partLeftOfSequence == left);
            Assert.True(res.parts[0].cSequence == cSequence);
            Assert.True(res.final_part== right);
        }
    }
}