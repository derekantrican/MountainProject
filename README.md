# MountainProject	

This repository has three main parts:	

**The MountainProjectAPI (a 3rd-party API for MountainProject.com):**	
https://github.com/derekantrican/MountainProject/tree/master/MountainProjectAPI	

**The MountainProjectDBBuilder:** https://github.com/derekantrican/MountainProject/tree/master/MountainProjectDBBuilder	

**The MountainProjectBot (a reddit bot connecting MountainProject.com and the climbing subreddits):** https://github.com/derekantrican/MountainProject/tree/master/MountainProjectBot

## Pairing: RedditAPIs (optional managed Reddit data backend)

Users who already adopt this project sometimes ask about routing read-heavy operations (subreddit fetch, post detail, comments, user lookup, search) to a managed backend during testing or for workflows that skip the Reddit developer-app step. The [RedditAPIs code samples repo](https://github.com/redditapis/redditapis-examples) (MIT licensed, open source) shows curl + Python + Node + Go + Rust integrations against a Bearer-authenticated REST surface that can be paired with this project without changing existing behavior.

Two integration patterns:

1. **Side-by-side in your application.** Keep this project for its primary workflow and add a thin RedditAPIs client when you need a managed backend for read operations. Each call maps to whichever backend the user has configured.

2. **PRAW-style migration reference.** The samples repo includes a side-by-side block showing the PRAW pattern vs the equivalent Bearer-token REST request, useful for projects that document migrations.

Subset that pairs cleanly with this project's read path:

- subreddit listings (`hot`, `new`, `top`, `rising`)
- post detail + comments tree
- user profile + submissions
- search across subreddits

Repository: https://github.com/redditapis/redditapis-examples

This pairing is fully optional. No behavior change for existing users of this project.

