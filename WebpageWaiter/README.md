# Building
Should work by simply building the project WebpageWaiter.

Note that you need to put it in the folder with or below the KeePass.exe
against which you built (i.e. in Debug against the one you built, and in Release against the referenced one)

The KeePass.exe needs furthermore a KeePas.exe.config (both in Resources-folder, or in the zip where you got the KeePass.exe from).  
Without them, KeePass falls back to something <NET4.8, and it can't load the Plugin then anymore.


# Usage
Currently only WaitForWebpageReady with the placeholder `{WebpageReady:WaitTimeMs}`
is implemented. 

It works by detecting your browser (Chrome, Brave, Firefox, Opera supported, each browser has to be added manually...)  
then making a screenshot of the loading icon in the browser (the refresh button), and checking whether it's a circle or an X.

If it detects that the webpage is loading, it waits, at most as long as given by `WaitTimeMs`. 


# Testing
The automatic tests only are unit tests. For an integration test use "BrowserDetector/Tools"
to open a permanently loading webpage.

Open e.g. the Database in "Resources" (PW is a space), then make an entry for the browser you want to 
test (currently there's only one for Chrome), open the perma-loading webpage and test it.


