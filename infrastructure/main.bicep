targetScope = 'resourceGroup'

@description('Location for all resources')
param location string = 'uksouth'

@description('Base name for resources')
param baseName string = 'expensemgmt${uniqueString(resourceGroup().id)}'

@description('Whether to deploy GenAI resources')
param deployGenAI bool = false

// Deploy App Service with Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: baseName
  }
}

// Conditionally deploy GenAI resources
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    location: location
    baseName: baseName
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output managedIdentityName string = appService.outputs.managedIdentityName
output managedIdentityClientId string = appService.outputs.managedIdentityClientId

// GenAI outputs (only available when deployed)
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output openAIName string = deployGenAI ? genai.outputs.openAIName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
output searchName string = deployGenAI ? genai.outputs.searchName : ''
