# MountainProjectDBBuilder

MountainProject.com is the definitive online "guide book" for climbing routes (at least in America, but also has a fairly good directory for other countries). The problem is that the [Official API](https://www.mountainproject.com/data) is *extremely* limited.

There are a few other people who have attempted to create APIs or scrapers of MountainProject to improve on this (here are the few that I've found):

- https://github.com/GottBott/RockRoutesGPS (MountainProject GPS scraper written in Python)
- https://laughinggoat.github.io/mpdata.html (MountainProject "tick" scraper written in Python)
- https://github.com/rohanbk/Mountain-Project-Scraper (MountainProject scraper written in Python)
- https://github.com/nickroberts404/MountainScrape (another MountainProject scraper written in Python)
- https://github.com/rankrh/MountainProject (another MountainProject scraper written in Python)
- https://github.com/jlauters/OpenMPAPI (Unofficial MountainProject API written in JavaScript)
- https://github.com/berto/mountain-project (Another unofficial MountainProject API written in JavaScript)
- https://github.com/mastahyeti/mountain_project (Unofficial MountainProject API written in Ruby)
- ~~https://github.com/dcohen21/8a.nu-Scraper (8a.nu scraper written in Python)~~ (Taken down by DMCA, but data is still accessible [here](https://www.kaggle.com/dcohen21/8anu-climbing-logbook)

Most of these haven't been updated in a while. And I figured I'd learn something by creating my own. I'm familiar with C# so I decided to use that and [AngleSharp](https://anglesharp.github.io/) to do my own scraping.

------------

### Using the program:

- Download the repository and build the solution
- Once built, use cmd to run the program (eg `MountainProjectDBBuilder.exe -build`. You can see all options by using `MountainProjectDBBuilder.exe -help`). *Heads-up: building the DB will take a while (currently clocked at about 9 hours!)*
- Once finished, in the same location you should have a "MountainProjectAreas.xml" file of every route and area on MountainProject.com (here is the latest I have created: https://drive.google.com/open?id=12C0EWBpPLjjlDw0zan6Q6U5KZjaq5OxQ)

![screenshot](https://i.imgur.com/eCFjCRi.gif)

-------------

### Reporting issues & contributing:

**Reporting issues:** Please report issues via the issues page: https://github.com/derekantrican/MountainProjectScraper/issues

**Contributing:** Feel free to fork this repository, make some changes, and make a pull request! If you have a helpful change, I'll add you to the repository so you can contribute directly to it

**Donate to this project:** https://www.paypal.me/derekantrican
