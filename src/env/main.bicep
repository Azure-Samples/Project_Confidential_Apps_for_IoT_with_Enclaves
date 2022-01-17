targetScope = 'resourceGroup'

var suffix = uniqueString(resourceGroup().id)

resource akv 'Microsoft.KeyVault/vaults@2021-06-01-preview' = {
  name: 'vault${suffix}'
  location: resourceGroup().location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enabledForTemplateDeployment: true
    tenantId: subscription().tenantId
    accessPolicies: []
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: 'insights'
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource blob 'Microsoft.Storage/storageAccounts@2021-06-01' = {
  name: 'storage${suffix}'
  location: resourceGroup().location
  kind: 'Storage'
  sku: {
    name: 'Standard_LRS'
  }
}

resource farm 'Microsoft.Web/serverfarms@2021-02-01' = {
  name: 'farm'
  location: resourceGroup().location

  kind: 'linux'
  sku: {
    name: 'S1'
    tier: 'Standard'
    capacity: 0
  }
}

var connectionString = 'DefaultEndpointsProtocol=https;AccountName=${blob.name};AccountKey=${listKeys(blob.id, blob.apiVersion).keys[0].value};EndpointSuffix=core.windows.net'
resource func 'Microsoft.Web/sites@2020-12-01' = {
  name: 'func${suffix}'
  location: resourceGroup().location
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: farm.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: connectionString
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: connectionString
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower('func${suffix}')
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: insights.properties.InstrumentationKey
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'HubConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${hubConnectionStringSecret.properties.secretUri})'
        }
        {
          name: 'KeyVaultEndpoint'
          value: '@Microsoft.KeyVault(SecretUri=${akvUrlSecret.properties.secretUri})'
        }
      ]
      use32BitWorkerProcess: false
      linuxFxVersion: 'DOTNET|6.0'
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource app 'Microsoft.Web/sites/sourcecontrols@2021-02-01' = {
  name: 'web'
  parent: func

  properties: {
    repoUrl: 'https://github.com/JimPaine/Project_Confidential_Apps_for_IoT_with_Enclaves.git'
    branch: 'main'
    isManualIntegration: true
  }
}

var secretOfficer = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
resource funcRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid('funcRoleAssignment${suffix}')
  properties: {
    principalId: func.identity.principalId
    roleDefinitionId: secretOfficer
  }
  scope: akv
}

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-01' = {
  name: 'iot${suffix}'
  location: resourceGroup().location
  sku: {
    name: 'S1'
    capacity: 1
  }
  properties: {
    eventHubEndpoints: {
      events: {
        partitionCount: 2
         retentionTimeInDays: 1
      }
    }
  }
}

var sharedAccessKey = listKeys(iotHub.id, iotHub.apiVersion).value[0].primaryKey
resource hubConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'HubConnectionString'
  parent: akv

  properties: {
    value: 'Endpoint=${iotHub.properties.eventHubEndpoints.events.endpoint};SharedAccessKeyName=iothubowner;SharedAccessKey=${sharedAccessKey};EntityPath=${iotHub.name}'
  }
}

resource akvUrlSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'KeyVaultEndpoint'
  parent: akv

  properties: {
    value: akv.properties.vaultUri
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2021-06-01-preview' = {
  name: 'acr${suffix}'
  location: resourceGroup().location
  sku: {
    name: 'Standard'
  }
}
