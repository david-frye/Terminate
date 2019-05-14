# Terminate
Terminate is a diagnostics and recovery tool designed to help LANDesk administrators.

# Usage:
  Terminate.exe TARGET=target-process-name TTL=max-process-age-in-minutes CONTRACT=TAG-or-KILL
  
  process stop example: 
  Terminate.exe TARGET=vulscan.exe TTL=15 CONTRACT=KILL
  
  process report example: 
  Terminate.exe TARGET=vulscan.exe TTL=15 CONTRACT=TAG
  
# Notes:
  Terminate command parameters are case insensitive.  Process name can be specified with or without the .exe extension.
  
  Terminate logs output to the console window (when run interactively) as well as to a log file at %TEMP% which will resolve to Windows\\Temp if run as a LANDesk task
  
  Using the 'tag' parameter enables the LANDesk administrator to view process info centrally from the LANDesk console.  However, to use this feature, you must enable custom data.  When ‘tag’ is used, terminate.exe will use miniscan.exe to send custom data from the client to the LANDesk core at these two custom data paths:  
  
  Custom Data - Support - ProcessName
  
  Custom Data - Support - ProcessAgeMinutes
