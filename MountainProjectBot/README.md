# MountainProjectBot

This is a reddit bot for connecting [MountainProject.com](https://mountainproject.com) and the [/r/climbing](https://reddit.com/r/climbing) subreddit.

**Feedback:** https://forms.gle/RKiU2Bzy9gXg4VBw8

**Donate to this project:** https://www.paypal.me/derekantrican

### ----Currently this bot is "beta" and you can test it out on https://reddit.com/r/MountainProjectBot/ ----

-----------
### Roadmap

**Stage 0 (beta):** Same as Stage 1, but on [/r/MountainProjectBot](https://reddit.com/r/MountainProjectBot/) as a "test environment"

**[Stage 1:](https://github.com/derekantrican/MountainProject/milestone/1)** Implement the bot on [/r/climbing](https://reddit.com/r/climbing) with "popularity" filtering and partial matching (case-insensitive). *Popularity filtering:* if multiple routes/areas match a search term, we will attempt to return the intended one by using the "page views" value published on MountainProject. *Partial matching:* eg "Deception Crags" will match "Exit 38: Deception Crags"

**[Stage 2:](https://github.com/derekantrican/MountainProject/milestone/2)** Upgrade the bot to allow matching based on [multiple input criteria](https://github.com/derekantrican/MountainProject/issues/8)

**[Stage 3:](https://github.com/derekantrican/MountainProject/milestone/3)** *(theoretical)* Upgrade the bot to use ["fuzzy string matching"](https://github.com/derekantrican/MountainProject/issues/7) to account for mispellings or missed/extra words

**[Stage 4:](https://github.com/derekantrican/MountainProject/milestone/4)** *(theoretical)* The eventual, end goal. Automatically parse [/r/climbing](https://reddit.com/r/climbing) submission titles for route/area names and automatically comment on the post with the route/area information. This would elimate the situation that currently happens: people commenting "where is this?", "what difficulty?", etc

-----------
### Reporting issues & contributing:

**Reporting issues:** Please report issues via the issues page: https://github.com/derekantrican/MountainProjectScraper/issues

**Contributing:** Feel free to fork this repository, make some changes, and make a pull request! If you have a helpful change, I'll add you to the repository so you can contribute directly to it
