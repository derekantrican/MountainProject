# MountainProject API

*This is a C# API for the website [MountainProject.com](https://mountainproject.com). For a brief history of why this exists or other attempts to create a 3rd-party MountainProject API, go here: https://github.com/derekantrican/MountainProject/blob/master/MountainProjectDBBuilder/README.md*

### How To Implement the API in your Project

1. Clone the [MountainProjecAPI folder](https://github.com/derekantrican/MountainProject/tree/master/MountainProjectAPI) to your computer
2. Add the MountainProjectAPI.csproj to your project

*I intend to release this as a nuget package at some point to make implementation even easier.*

### How to Use the API

1. Currently, the only place to start with the API is to load in a serialized version of all the data. You can get that in one of 3 ways:
    - Download from here: https://drive.google.com/open?id=12C0EWBpPLjjlDw0zan6Q6U5KZjaq5OxQ
    - [Contact me](mailto:derekantrican@gmail.com) if you want a more recent copy
    - Build your own using the [MountainProjectDBBuilder](https://github.com/derekantrican/MountainProject/tree/master/MountainProjectDBBuilder)
2. Load in the data by calling `MountainProjectDataSearch.InitMountainProjectData(PATH_TO_FILE)`
3. Use the `MountainProjectDataSearch.Search(string queryText, SearchParameters searchParameters = null)` method to search the data or write your own to find what you need in the full tree (`MountainProjectDataSearch.DestAreas`)

### Structure

Objects on MountainProject.com are structured like a tree:

- Destination Area *(any US State and the "International" area)*

  - Area *(any other area or wall)*

    - Route
    
*By [MountainProject's own definition](https://i.imgur.com/e6ZEP65.png), Areas cannot contain both sub-Areas and Routes. But this API is flexible enough that, if that were to change, no adjustments to this API would be required.*

The basic structural objects of the API are `Route` and `Area`:

#### Area.cs
```csharp
	public AreaStats Statistics { get; set; }
	public List<Area> SubAreas { get; set; }
	public List<Route> Routes { get; set; }
	public List<string> PopularRouteIDs { get; set; }
	
	public List<Route> GetPopularRoutes(int numberToReturn)
```

#### Route.cs
```csharp
	public double Rating { get; set; }
	public List<Grade> Grades { get; set; }
	public List<RouteType> Types { get; set; }
	public string TypeString { get; }
	public string AdditionalInfo { get; set; }
	public Dimension Height { get; set; }
	
	public Grade GetRouteGrade(GradeSystem requestedSystem = GradeSystem.YDS)
```

There are obviously other objects (`Grade`, `Dimension`, amd `AreaStats`) but they should be pretty straight-forward.

------------
*This is a 3rd party API and is not endorsed at all by MountainProject*
