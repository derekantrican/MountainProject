@ECHO OFF
setlocal

SET CUR=%~dp0

REM Restart the bot every night
schtasks /create /sc daily /st 01:00 /tn "Restart MPBot every night" /tr %CUR%RestartMPBot.bat

set CUR=%CUR%..\MountainProjectDBBuilder\

REM Update the DB every night at 11pm
schtasks /create /sc daily /st 23:00 /tn "Update MountainProjectAreas.xml with new data" /tr "%CUR%bin\MountainProjectDBBuilder.exe --onlyNew"

REM Do a full update of the DB once per week
schtasks /create /sc weekly /st 01:00 /d SUN /tn "Rebuild MountainProjectAreas.xml" /tr %CUR%bin\MountainProjectDBBuilder.exe