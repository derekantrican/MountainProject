@taskkill /f /im MountainProjectBot.exe >nul
@timeout /t 3 /nobreak >nul
@start bin\MountainProjectBot.exe xmlpath=..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml repliedto=bin\RepliedTo.txt repliedtoposts=bin\RepliedToPosts.txt blacklisted=bin\BlacklistedUsers.txt >nul