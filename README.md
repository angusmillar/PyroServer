# Pyro Server
FHIR R4 Server Implementation

#### Setup steps::

1. Restore all nuget packages
2. Update the ```Database.ConnectionString``` file path in the ```appsettings.Development.json``` file in the Sonic.Pyro.Api project
3. Install the [Entity Framework Core tools](https://learn.microsoft.com/en-us/ef/core/get-started/overview/install#get-the-entity-framework-core-tools) cmd:```dotnet tool install --global dotnet-ef```
4. Run [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#command-line-tools) Update command in the Sonic.Pyro.Repository project folder to create the database. cmd:```dotnet ef database update```
5. Run the Sonic.Pyro.Api project


