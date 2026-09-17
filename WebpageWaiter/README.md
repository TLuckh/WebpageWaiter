# Building
Should work by simply building the project WebpageWaiter.

Note that you need to put it in the folder with or below the KeePass.exe
against which you built (i.e. in Debug against the one you built, and in Release against the referenced one)

The KeePass.exe needs furthermore a KeePas.exe.config (both in Resources-folder, or in the zip where you got the KeePass.exe from).  
Without them, KeePass falls back to something <NET4.8, and it can't load the Plugin then anymore.


# Testing
The automatic tests only are unit tests. For an integration test use "BrowserDetector/Tools"
to open a permanently loading webpage.

Open e.g. the Database in "Resources" (its PW is a single space), then make an entry for the browser you want to
test (currently there's only one for Chrome), open the perma-loading webpage and test it.


# Adding new Browsers
Note: Limited by the design of the Plugin, only Browsers which have a loading button can be added.

To add a browser:

1. Download `FlaUInspect.exe`from https://github.com/FlaUI/FlaUInspect
2. Open your Browser you want to add
3. Open from Tools the webpage after starting the .py in the folder.
4. Possibly, you have to refresh once manually
5. In FlaUInspect.exe.
6. Choose the Browser
7. Activate HoverMode
8. Hover over the Loading Button, then press Ctrl
9. Note:

- FrameworkId
- ClassName
- AutomationIdLoaded
- AutomationIdLoading
- ControlType

10. Repeat for the other Loading-Button state (i.e. test once when the loading button showsthe circle, once when it shows the X)

Now add your findings:
1. Create a another Property in the class BrowserLoadingButtons in BrowserLoadingDetector.csproj
2. Add the name of the browser in   `#region SwitchOnBrowser` as switch-case:  
   Use the name which is given by FlaUInspect.exe when choosing the Browser Window as its ending (e.g. for Chrome: New Tab - Chrome)

Build the program and test if it's working.