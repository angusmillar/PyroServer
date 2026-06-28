# Pyro FHIR Server

.NET FHIR R4.0.1 Server Implementation

#### Setup steps::

1. Restore all nuget packages
2. Update the ```ConnectionStrings.PyroDb``` found in the ```Abm.Pyro.Api``` project's ```appsettings.Development.json``` file 
3. Install the [Entity Framework Core tools](https://learn.microsoft.com/en-us/ef/core/get-started/overview/install#get-the-entity-framework-core-tools) cmd:```dotnet tool install --global dotnet-ef```
4. Run [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#command-line-tools) Update command in the Abm.Pyro.Repository project folder to create the database. cmd:```dotnet ef database update```
5. Run the Abm.Pyro.Api project



```                                                                                                    
                                        ...:=*#%@@@%#*+-...                                         
                                   ..=%@@@@@@@@@@@@@@@@@@@@@@#-.                                    
                                .-%@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@*..                                
                              .*@%@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@%-                               
                            ...:::::...  .:*@@@@@@@@@@@#-.  ...:::::...                      
                          .:.          .:**:..*@@@@@%...=#-.          .-.                   
                          :.               .#-.=@@@*..#:.              .-                     
                         .:.                .%..%@@-.#:                 ..                    
                         .:                 .%.:@@@=.*-                 .:                          
                         ..                .#-.+@@@%..#.                .:                          
                         .:.              .#-.=@@@@@#..#:               .:                          
                         .:.             :%..*@@@@@@@%..++              :.                    
                          .:.         .:#:..%@@@@@@@@@@- .#-.          .:                    
                           .::.    ..=#. .#@@@@@@@@@@@@@@:..+*..    ..-.                      
                          .==..:=+=:...-%@@@@@@@@@@@@@@@@@@=. .:=++-..-#.                           
                      ..-=..*@@#+==+%@@@@@@@@@=......@@@@@@@@@%*==+#@@@:.:=:.                       
                ..:*#=..-@@:.*@@@@@@@@@@@@@@@@=     .@@@@@@@@@@@@@@@@@:.+@#..:*#=...                
           .:+#+:.       -@@:.+-+@@@@@@@@-::::.     .:----%@@@@@@@@:+-.+@#.      ..-**-.            
           .%@=.          =@%:#@-+@@@@@@@.               .%@@@@@@@:#@-*@#.          .%@=            
            .@@=          .-@%:#@-+@@@@@@.               .%@@@@@@:#@-+@%.          .%@=.            
             :@@=          .=@%:#@-+@@@@@@@@@@=     .@@@@@@@@@@@:#@-+@#.          .%@=.             
             :@@@=          .=@@:#@:+@@@@@@@@@=     .@@@@@@@@@@:#@-+@#.          .%@@=.             
              :@@@=          .=@%:%@-*@@@@@@@@@@@@@@@@@@@@@@@@:%@-+@#.          .%@@=.              
               .@@@-.         .=@%.....*@@@@@@@@@@@@@@@@@@@%-. ..+@%.          .%@@+.               
                .@@@-.         .=@%.      ..-+#%@@@@%*=:.       +@%.          .@@@+.                
                 .#@@-.         .=@%.                          +@%.          .%@@-.                 
                  ..@@-.     ..-**-.                           .:+#=:.      .%@+.                   
                    .@@-.:*#=..                                     ..:*#=..%@+.                    
                     .-:.                                                 ..-:                                                                                                                                         
```


