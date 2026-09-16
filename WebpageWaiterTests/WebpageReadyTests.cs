using KeePass.Util;
using NUnit.Framework;
using WebpageWaiter;

namespace WebpageWaiterTests
{
    public class WebpageReadyTests
    {

        [Test]
        public void ParsingTest()
        {
            string relevantCSequence = "WeBPageReAdy";          // Casing doesn't matter
            int maxWaitTime       = 100;
            
            // Pre and post-stuff doesn't matter (though the input always only contains one valid match)
            string            left     = $"Ipsor Lorem {{Sample}}´ ";
            string            middle   = $"{{{relevantCSequence}:{maxWaitTime}}}";
            string            right    = $"!\"§%%&%$\"&";
            string            sequence = left + middle + right;
            AutoTypeEventArgs e        = new AutoTypeEventArgs(sequence,false,null,null);
            CSequence.TryParse(e.Sequence,out CSequence _cSequence);
            
            Assert.True(_cSequence is WaitForWebpageReady);
            var cSequence = (WaitForWebpageReady)_cSequence;
            Assert.True(cSequence.MaxWaitTime == maxWaitTime);
            

        }
    }
}