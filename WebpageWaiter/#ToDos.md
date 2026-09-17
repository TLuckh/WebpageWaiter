- ToDo: For all remaining methods but the URL-check, we can make good use of FlaUIs capability of returning the center point, so we can enumerate all as editable (pw) fields from left to right, from top to bottom.
- ToDo: Make HoughCircle only check center-points of the circle near the center of the picture? Not sure tbh
- ToDo: Ship as Plugin-Extension-Format?

-	For Firefox and whatever other browsers there are which give the reload button specific automationIds that differ depending on the state, add a code path on WebpageReady that just checks these and foregoes picture scanning
-	Add more unit tests:
- -	Enrich tests by capturing and serializing real browser instance UI-hierarchies as given by FlaUI.
I.e. get the whole UI-tree as e.g. given by FlaUInspect.exe for the tab, and then use FlaUInspect’s Hover-Mode to select the button and then get its XPath to check against.
-  -	Do the same for actual pictures of loading buttons as given by FlaUI for different Browsers with different themes, different Windows DPI and resolution settings.


See the ToDos in the *.cs, especially CSequences for implementations yet to be done