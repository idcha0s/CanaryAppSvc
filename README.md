# CanaryAppSvc

This is a sample test harness in ASP.NET Core 6.0 providing a Web App to run in App Service fetching secrets from various Azure Key Vaults. This harness is designed to generate signal for testing and validating changes to the vault resource firewall, service endpoint and private endpoint configurations.

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

## Managed Identity

The client uses DefaultAzureCredential() to connect to the remote vault - no secrets are needed in the configuration to authenticate. You must ensure that your App Service has been configured to use a System Assigned Managed Identity. You will be able to identify the object ID of your MI in the logs in the `oid` field. Refer to the AzureDiagnostics Query below.

This should also work during debug as it should pull your Azure user principal. To authenticate to the App Service using Azure AD, make sure you've enabled Authentication on your App Service for your AAD tenant.

## AzureDiagnostics logging

To make testing easier, it is recommended that you enable Diagnostics logging on each vault, and configure them to log to a common Log Analytics workspace. From that workspace you can run the following query to see how each vault is being accessed:

```kusto
AzureDiagnostics
// Build src_network based on addrAuthType_s, subnetId_s, privateEndpointId_s, isAddressAuthorized_b, OperationName, and httpStatusCode_d
| where OperationName == "SecretGet"
| where ResourceProvider =="MICROSOFT.KEYVAULT"
| project-rename oid=identity_claim_oid_g, upn=identity_claim_upn_s, app_id=identity_claim_appid_g
| extend src_network = case(addrAuthType_s == "PublicIP", "dataPlane/resourceFirewall/internet", 
    addrAuthType_s == "TrustedService", "dataPlane/resourceFirewall/TrustedService",
    isnotempty(subnetId_s) and addrAuthType_s == "Subnet", strcat("dataPlane/serviceEndpoints/",split(subnetId_s,"/",8)[0],"/",split(subnetId_s,"/",10)[0]),
    isnotempty(privateEndpointId_s),strcat("dataPlane/",split(privateEndpointId_s,"/",7)[0],"/",split(privateEndpointId_s,"/",8)[0]),
    isempty(isAddressAuthorized_b) and httpStatusCode_d == 200 and OperationName in ("GetPrivateLinkResources","GetPrivateEndpointConnectionProxy","PostPrivateEndpointConnectionProxyValidate","PutPrivateEndpointConnectionProxy","Authentication","VaultGet","VaultPut","VaultDelete","VaultPatch","VaultList","VaultPurge","VaultRecover","VaultGetDeleted","VaultListDeleted","VaultAccessPolicyChangedEventGridNotification"),strcat("controlPlane/", OperationName),
    isempty(isAddressAuthorized_b) and httpStatusCode_d == 401,strcat("controlPlane/", OperationName),
    (isempty(isAddressAuthorized_b) or isAddressAuthorized_b == "false") and httpStatusCode_d == 403,"dataPlane/resourceFirewall/internet/denied",
    isempty(httpStatusCode_d),strcat("controlPlane/", OperationName),
    (isempty(isAddressAuthorized_b) and httpStatusCode_d == 200 and OperationName !in ("GetPrivateLinkResources","GetPrivateEndpointConnectionProxy","PostPrivateEndpointConnectionProxyValidate","PutPrivateEndpointConnectionProxy","Authentication","VaultGet","VaultPut","VaultDelete","VaultPatch","VaultList","VaultPurge","VaultRecover","VaultGetDeleted","VaultListDeleted","VaultAccessPolicyChangedEventGridNotification")),strcat("dataPlane/insufficient_info"),
    isnotempty(httpStatusCode_d),strcat("unknown/", OperationName, "/", tostring(httpStatusCode_d)),
    "")
| project TimeGenerated, OperationName, oid, app_id, CallerIPAddress, src_network, Resource, httpStatusCode_d, CorrelationId
| order by TimeGenerated
```

## Azure App Services

You can deploy this application into as many regions as you'd like to simulate traffic to, just ensure that you are enabling virtual network integration for each instance of your App Service and that you have a subnet delegated to it. It is also helpful to direct your diagnostics logging for your App Service instances to the same Log Analytics workspace. You can query for the AppService logs like so:

If you are doing inbound restrictions to your App Service you can query the AppServiceIPSecAuditLogs to see which rule allowed access to the web app:

```kusto
AppServiceIPSecAuditLogs 
| order by TimeGenerated
```

To see traffic from the App Service health checks as well as your external access requests allowed through the App Service resource firewall, use the AppServiceHTTPLogs as such:

```kusto
AppServiceHTTPLogs 
| project TimeGenerated, CsMethod, CsUriStem, CIp, ComputerName, TimeTaken, UserAgent, ScStatus, CsUriQuery, Referer
| order by TimeGenerated
```
