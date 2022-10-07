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
