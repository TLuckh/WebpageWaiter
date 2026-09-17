# WebWaiter - A KeePass Plugin
## Usage
Currently, only the placeholder `{WebpageReady:WaitTimeMs}`
is implemented.  
When used in an auto-type sequence in KeePass, it'll wait at most `WaitTimeMs` for the webpage to load. 

It works by detecting your browser (Chrome, Brave, Firefox, Opera supported, each browser has to be added manually...)  
then making a screenshot of the loading icon in the browser (the refresh button), and checking whether it's a circle or an X.

## Developer Info
See WebpageWaiter's README.md for the main project. 
 
