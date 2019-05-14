## Terminate
### Summary
On occasion, Windows processes running on managed endpoints will fail to close in a timely manner resulting in resource contention and subsequent LANDesk job failures on those endpoints.  Terminate is a simple command line utility that can either force close a named client process, or report back to LANDesk inventory on hung process occurrence.  Terminate will only manage processes that are running on the local computer on which it is executed.

### Usage:
  Terminate.exe TARGET=target-process-name TTL=max-process-age-in-minutes CONTRACT=TAG-or-KILL
  
  #### Process stop example
  In this example we use Terminate to force close (kill) all instances of vulscan that have been running for more than 15 minutes.
  
       Terminate.exe TARGET=vulscan.exe TTL=15 CONTRACT=KILL
       
  
  #### Process report example: 
  In this example we use Terminate to report all instances of vulscan that have been running for more than 15 minutes.
  
       Terminate.exe TARGET=vulscan.exe TTL=15 CONTRACT=TAG
  
### Notes:
  Terminate command parameters are case insensitive.  Process name can be specified with or without the .exe extension.
  
  Terminate logs output to the console window (when run interactively) as well as to a log file at %TEMP% which will resolve to Windows\\Temp if run as a LANDesk task
  
  Using the 'tag' parameter enables the LANDesk administrator to view process info centrally from the LANDesk console.  However, to use this feature, you must enable custom data.  When ‘tag’ is used, terminate.exe will use miniscan.exe to send custom data from the client to the LANDesk core at these two custom data paths:  
  
  Custom Data - Support - ProcessName
  
  Custom Data - Support - ProcessAgeMinutes
