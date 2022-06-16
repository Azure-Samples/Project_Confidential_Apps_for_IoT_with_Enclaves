targetScope = 'resourceGroup'

var suffix = uniqueString(subscription().id, resourceGroup().id)

param location string = resourceGroup().location
@description('Use the recommended approach for managing rbac around AKV. You will need to set this to false if you do not have the appropriate permissions to manage roles in a subscription.')
param keyvault_use_rbac bool = true

resource akv 'Microsoft.KeyVault/vaults@2021-06-01-preview' = {
  name: 'vault${suffix}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: keyvault_use_rbac
    enabledForTemplateDeployment: true
    tenantId: subscription().tenantId
    accessPolicies: []
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: 'insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource blob 'Microsoft.Storage/storageAccounts@2021-06-01' = {
  name: 'storage${suffix}'
  location: location
  kind: 'Storage'
  sku: {
    name: 'Standard_LRS'
  }
}

resource funIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: 'func-id-${suffix}'
  location: location
}

resource farm 'Microsoft.Web/serverfarms@2021-02-01' = {
  name: 'farm'
  location: location

  sku: {
    name: 'S1'
    tier: 'Standard'
  }
}

var connectionString = 'DefaultEndpointsProtocol=https;AccountName=${blob.name};AccountKey=${listKeys(blob.id, blob.apiVersion).keys[0].value};EndpointSuffix=core.windows.net'
resource func 'Microsoft.Web/sites@2020-12-01' = {
  name: 'func${suffix}'
  location: location
  kind: 'functionapp'
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
          // Using Azure Key Vault references so we can use the managed identity to pull
          // secrets from key vault and inject them into our app environment without needing
          // a code dependency on the key vault or AAD.
          //
          // https://docs.microsoft.com/en-us/azure/app-service/app-service-key-vault-references
          value: '@Microsoft.KeyVault(SecretUri=${hubConnectionStringSecret.properties.secretUri})'
        }
        {
          name: 'KeyVaultEndpoint'
          value: '@Microsoft.KeyVault(SecretUri=${akvUrlSecret.properties.secretUri})'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: funIdentity.properties.clientId
        }
      ]
      use32BitWorkerProcess: false
      alwaysOn: true
    }
    keyVaultReferenceIdentity: funIdentity.id
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${funIdentity.id}': {}
    }
  }
}

// add source control settings so that we can use kudu to handle the application
// compilation and deployment.
resource app 'Microsoft.Web/sites/sourcecontrols@2021-02-01' = {
  name: 'web'
  parent: func

  properties: {
    repoUrl: 'https://github.com/Azure-Samples/Project_Confidential_Apps_for_IoT_with_Enclaves.git'
    branch: 'main'
    isManualIntegration: true
  }
}

// create role assignment that allows our function app to manage secrets in the key vault.
// This is so we can user key vault reference for application settings (mentioned above) and
// So in code we can use managed identity to create and retrieve secrets from key vault.
var secretOfficer = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
resource funcRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (keyvault_use_rbac) {
  name: guid('funcRoleAssignment${suffix}', func.id)
  properties: {
    principalId: funIdentity.properties.principalId
    roleDefinitionId: secretOfficer
  }
  scope: akv
}

resource accessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2021-06-01-preview' = if (!keyvault_use_rbac) {
  parent: akv
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: funIdentity.properties.principalId
        permissions: {
          secrets: [
            'get'
            'list'
            'set'
          ]
        }
      }
    ]
  }
}

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-01' = {
  name: 'iot${suffix}'
  location: location
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

// Create secrets for consumed resources and put their keys / connections strings into key vault
// so that the function app can consume them using the key vault references noted above.
var sharedAccessKey = listKeys(iotHub.id, iotHub.apiVersion).value[0].primaryKey
resource hubConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'HubConnectionString'
  parent: akv

  properties: {
    value: 'HostName=${iotHub.properties.hostName};SharedAccessKeyName=iothubowner;SharedAccessKey=${sharedAccessKey}'
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
  location: location
  sku: {
    name: 'Standard'
  }
}

resource provisioningService 'Microsoft.Devices/provisioningServices@2021-10-15' = {
  name: 'ps${suffix}'
  location: location
  sku: {
    name: 'S1'
    capacity: 1
  }
  properties: {
    enableDataResidency: false
    iotHubs: [
      {
        location: location
        connectionString: 'HostName=${iotHub.properties.hostName};SharedAccessKeyName=iothubowner;SharedAccessKey=${sharedAccessKey}'
      }
    ]
  }
}
