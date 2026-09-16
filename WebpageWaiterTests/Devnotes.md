# Foregone Choices
## Caching button state using UIAutomation-Properties
Technically could be done easily, e.g. via


```        var newCache =  new ButtonState();
    private class ButtonState()
    {
        public string AutomationId { get; set; } 
        public string Name { get; set; } 
        public string HelpText { get; set; } 
        
        
    }

newCache = new ButtonState();
if (button.Properties.AutomationId.IsSupported) newCache.AutomationId = button.Properties.AutomationId.ValueOrDefault ?? "";
if (button.Properties.Name.IsSupported) newCache.Name = button.Properties.Name.ValueOrDefault ?? "";
if (button.Properties.HelpText.IsSupported) newCache.HelpText = button.Properties.HelpText.ValueOrDefault ?? "";
if (newCache.Equals(cache)) continue; else cache = newCache;
```
Caching works temporally, since in the first run the previously cached state is null, 
so a change occurs, and we run the remainder of the function body.

Otherwise, since we haven't returned true, the state of the button is loading, so the button will change.

So far, on all tested Browsers, there was something changing:
HelpText changes technically always, but e.g. Firefox's HelpText can't be accessed in FlaUI...
In Firefox however, the automationId changes.

However, the chance that both HelpText isn't readable, and automationId stays constant is rather high.

Compared to that, saving the screenshot is less of a gamble.
