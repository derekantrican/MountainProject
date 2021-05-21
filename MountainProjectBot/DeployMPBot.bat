@ECHO OFF

REM pull latest code from github (overwriting any local changes)
git fetch --all
git reset --hard origin/master

REM restore nuget packages
dotnet restore ..\

REM Build sln
dotnet build  ..\MountainProject.sln
if %errorlevel% neq 0 exit /b %errorlevel%

REM Run bot to update the replied txt files
bin\MountainProjectBot.exe dryrun xmlpath=..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml repliedto=bin\RepliedTo.txt repliedtoposts=bin\RepliedToPosts.txt blacklisted=bin\BlacklistedUsers.txt

REM Start bot normally
RestartMPBot.bat