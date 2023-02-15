# Spresso.Sdk

This repository contains all of the .net SDKs for calling the [Spresso](https://www.spresso.com/) API.


| Package | Link |
|---------|------|
| Core SDK | [![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Spresso.Sdk.Core)](https://www.nuget.org/packages/Spresso.Sdk.Core) |
| Price Optimizations | ![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Spresso.Sdk.PriceOptimizations) |


## Spresso.Sdk.Core
Core SDKs that deal with
* Authentication
* Resilient API calls

Note that for best performance, `AuthTokenHandler` should should be a singleton and live for the lifetime of the application.

## Spresso.Sdk.PriceOptimizations
SDK that works with the Spresso Price Optimzation APIs to fetch optimal prices for a given set of products.

Note that for best performance, `PriceOptimizationsHandler` should should be a singleton and live for the lifetime of the application.

By default this SDK tries it's best to always return an answer in a fixed amount of time, using default pricing.  The `IsSuccess` will let you know if the response came from the API or was the fallback price.  The `Error` property will inform you of what went wrong.

Fallback behavior can be disabled by setting `PriceOptimizationsHandlerOptions.ThrowOnFailure` to `true`.

This SDK will handle tokens for you and will return the default price if a bot is detected.

### Quickstart
```csharp
var authTokenHandler = new AuthTokenHandler("myClientId", "mySecret");
var priceOptimizationHandler = new PriceOptimizationsHandler(authTokenHandler);

// get a single price optimization
var response = await priceOptimizationHandler.GetPriceOptimizationAsync(new GetPriceOptimizationRequest(deviceId: "device123", itemId: "item42", defaultPrice: 9.99m, userId: "9635345345534ad3", overrideToDefaultPrice: false, userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36"));

// get price optimizaitons in a batch
var batchResponse2 = priceOptimizationHandler.GetBatchPriceOptimizationsAsync(
    new GetBatchPriceOptimizationsRequest(new[]
    {
        new GetPriceOptimizationRequest(deviceId: "123", itemId: "abc", defaultPrice: 19.99m, userId: "u42",
            overrideToDefaultPrice: false),
        new GetPriceOptimizationRequest(deviceId: "456", itemId: "xyz", defaultPrice: 11.99m, userId: "u42",
            overrideToDefaultPrice: false)
    }, userAgent: "google-bot"));

```
## Mock Server
This repository included a Mock Server that can be used for testing different SDKs (in any language), for testing integrations, and for testing error scenarios.

### To Run:
``` bash
cd ./tests/Spresso.MockApi && dotnet run
```

### Forced Error Conditions:
#### To test different response codes, add the following query parameters to the web request:
`status=<status code>` - will return the specified status code

example: `https://localhost:7176/pim/v1/priceoptimizations?status=500`

#### To test different response times, add the following query parameters to the web request:
`delay=<delay in seconds>` - will delay the response by the specified amount of time

example: `https://localhost:7176/pim/v1/priceoptimizations?delay=5`

Note: if testing via the SDK, you can use the `AdditionalParameters` property to add these query parameters to the request.

## Building
This solution is setup to use the [nuke](https://nuke.build/) build system (still a work in progress).

Assuming you have nuke [installed](https://nuke.build/docs/getting-started/installation/), you can run the following commands:


To build the solution, run `nuke` from the root of the repository.

To target a specific SDK, run `nuke --target-project <your sdk of choice>`

To build and run tests, run `nuke test` or `nuke test --target-project <your sdk>`

To get a list of sdks, run `nuke listsdks`

For help, run `nuke --help`

If you do not have nuke installed, you can also substitute `nuke` with `build.cmd`, `build.ps1`, or `build.sh`, depending on your platform.

## Benchmarks
To run benchmarks, build `tests/Spresso.Sdk.Benchmarks` in `Release` mode, then run `Spresso.Sdk.Benchmarks`.

[Historical Benchmarks](/tests/Spresso.Sdk.Benchmarks/History)
					   
