# MountainProjectBot

This is a reddit bot for connecting [MountainProject.com](https://mountainproject.com) and the [/r/climbing](https://reddit.com/r/climbing) subreddit.

## Currently this bot is "in progress" and not active

### Roadmap

**Stage 1:** Implement the bot on [/r/climbing](https://reddit.com/r/climbing) with *exact matching* only (eg must match the route/area exactly, case-insensitive allowed)
**Stage 2:** Upgrade the bot to use "popularity" filtering and attempt to return routes/areas by popularity (ie this means that if multiple routes/areas match a search term, we will attempt to return the intended one by using the "page views" value published on MountainProject)
**Stage 3:** (theoretical) Upgrade the bot to use "fuzzy string matching" to account for mispellings or missed/extra words. This way a spelling like "Red River Gourge" could hypothecially still return the information for "Red River Gorge".
**Stage 4:** The eventual, end goal. Automatically parse [/r/climbing](https://reddit.com/r/climbing) submission titles for route/area names and automatically comment on the post with the route/area information. This would elimate the situation that currently happens: people commenting "where is this?", "what difficulty?", etc

### Reporting issues & contributing:

**Reporting issues:** Please report issues via the issues page: https://github.com/derekantrican/MountainProjectScraper/issues

**Contributing:** Feel free to fork this repository, make some changes, and make a pull request! If you have a helpful change, I'll add you to the repository so you can contribute directly to it