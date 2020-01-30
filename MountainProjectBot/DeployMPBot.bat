@ECHO OFF

REM pull latest code from github (overwriting any local changes)
git fetch --all
git reset --hard origin/master

REM Build sln
"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\msbuild.exe"  ..\MountainProject.sln /p:Configuration=Debug

REM Run bot to update the replied txt files
bin\MountainProjectBot.exe dryrun xmlpath=..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml repliedto=bin\RepliedTo.txt repliedtoposts=bin\RepliedToPosts.txt blacklisted=bin\BlacklistedUsers.txt

REM Start bot normally
RestartMPBot.bat