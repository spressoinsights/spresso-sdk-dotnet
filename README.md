# Spresso.Sdk

This repository contains all of the .net SDKs for calling the [Spresso](https://www.spresso.com/) API.

A more detailed writeup is forthcoming.

### Spresso.Sdk.Core
Core SDKs that deal with
* Authentication
* Resilient API calls

Note that for best performance, `AuthTokenHandler` should should be a singleton and live for the lifetime of the application.

### Spresso.Sdk.PriceOptimizations
SDK that works with the Spresso Price Optimzation APIs to fetch optimal prices for a given set of products.

Note that for best performance, `PriceOptimizationsHandler` should should be a singleton and live for the lifetime of the application.

#### Quickstart
```csharp
var authTokenHandler = new AuthTokenHandler("myClientId", "mySecret");
var priceOptimizationHandler = new PriceOptimizationsHandler(authTokenHandler);
var response = await priceOptimizationHandler.GetPriceOptimizationAsync(new GetPriceOptimizationRequest(deviceId: "device123", itemId: "item42", defaultPrice: 9.99m, userId: "9635345345534ad3", overrideToDefaultPrice: false));
```

#### Building
This solution is setup to use the [nuke](https://nuke.build/) build system (still a work in progress).

Assuming you have nuke [installed](https://nuke.build/docs/getting-started/installation/), you can run the following commands:


To build the solution, run `nuke` from the root of the repository.

To target a specific SDK, run `nuke --target-project <your sdk of choice>`

To build and run tests, run `nuke test` or `nuke test --target-project <your sdk>`

To get a list of sdks, run `nuke listsdks`

For help, run `nuke --help`

If you do not have nuke installed, you can also substitute `nuke` with `build.cmd`, `build.ps1`, or `build.sh`, depending on your platform.