The core projects are WebpageWaiter and BrowserDetection.

The other two are artifacts from which I've used the  ideas. 
WebAutoType is probably still useful for URL detection.

For developing, KeePass_N48.csproj and KeePassLib_N48.csproj aren't really 
necessary anymore. They make debugging a little more comfortable, and 
throwing some doc-strings onto KeePass made it easier for me to not get lost.  
When building in Release-Mode, neither is used, and in Debug one just has 
to change them out for the .exe.

# Building
See same chapter in WebpageWaiter's README.md