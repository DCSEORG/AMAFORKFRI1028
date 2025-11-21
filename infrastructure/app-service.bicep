targetScope = 'resourceGroup'

@description('Location for all resources')
param location string

@description('Base name for resources')
param baseName string

// Get current timestamp for unique naming
var timestamp = utcNow('ddHHmm')

// User-assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'mid-AppModAssist-${timestamp}'
  location: location
}

// App Service Plan (low-cost development SKU)
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'plan-${baseName}'
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
    size: 'B1'
    family: 'B'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// App Service
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: 'app-${baseName}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ManagedIdentity__ClientId'
          value: managedIdentity.properties.clientId
        }
      ]
    }
  }
}

// Outputs
output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output managedIdentityName string = managedIdentity.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
