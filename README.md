# CanaryAppSvc

Sample test harness providing a Web App to run in App Service fetching secrets from various Azure Key Vaults. This harness is designed to generate signal for testing and validating changes to the vault resource firewall, service endpoint and private endpoint configurations.

By default the sample code provides an app.MapGet() for five pages, representing three sample vaults:

1. "/" - the root page with no active code. Use this page to test that the app is loading without calling a vault. Start by configuring your AppService health checks to this page.
1. "/cus" - sample - represents the page used to reach a vault in the Central US region.
1. "/eus" - sample - represents the page used to reach a vault in the East US region.
1. "/scus" - sample - represents the page used to reach a vault in the South Central US region.
1. "/all" - sample - renders all configured vaults in serial. You will configure your App Service health checks to target this page once everything is working correctly.

These pages allow for independent targeting regional vaults to improve testing and debugging. The primary goal is to generate telemetry in the AzureDiagnostics logs so you can prove that your application is accessing the vault using the appropriate connection (Resource Firewall|Service Endpoint|Private Endpoint). You will need to update the configuration in your appsettings.json file to target the vaults you use.

## appsettings.json

There are three sections in the json you'll need to configure with your vault information:

```json
  "Secret": "Canary",
  "Vaults": [
    "https://vault1-cus.vault.azure.net/",
    "https://vault2-scus.vault.azure.net/",
    "https://vault3-eus.vault.azure.net"  
    ],
  "RegionalVaults": {
      "/cus":  "https://vault1-cus.vault.azure.net/",
      "/scus": "https://vault2-scus.vault.azure.net/",
      "/eus":  "https://vault3-eus.vault.azure.net"  
  },
```

1. Secret - this is the name of the Secret the app will fetch from each vault
1. Vaults - this is the list that the /all page will cycle through on page load
1. RegionalVaults - this is the list parsed on individual targeted page load. The page will use the current path as the key to retrieve the vault name.

