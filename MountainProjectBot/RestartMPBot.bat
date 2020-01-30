@ECHO OFF

REM Kill MountainProjectBot.exe if it is running
tasklist /FI "IMAGENAME eq MountainProjectBot.exe" 2>NUL | find /I /N "MountainProjectBot.exe">NUL
if "%ERRORLEVEL%"=="0" taskkill /f /im MountainProjectBot.exe

REM Wait 3 seconds
@timeout /t 3 /nobreak >nul

REM Start up MountainProjectBot.exe again
@start bin\MountainProjectBot.exe xmlpath=..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml repliedto=bin\RepliedTo.txt repliedtoposts=bin\RepliedToPosts.txt blacklisted=bin\BlacklistedUsers.txt >nul