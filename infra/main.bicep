// Commit FHL — Azure Infrastructure
// Target: commit-fhl-rg (East US) in tenant 7k2cc2.onmicrosoft.com
// Resources: Storage, ACR, Container Apps Env, Container App, Static Web App
// Deploy: az deployment group create --resource-group commit-fhl-rg --template-file infra/main.bicep --parameters @infra/parameters.json

@description('Azure AD Tenant ID for the app registration')
param tenantId string = '91b9767c-6b0a-4b0b-bd4d-e08a6383426c'

@description('App registration Client ID (Commit API)')
param clientId string = '07b0afff-85b6-4be1-98ba-d26d566bd14a'

@description('App registration Client Secret')
@secure()
param clientSecret string

@description('Azure OpenAI endpoint (optional — NLP degrades gracefully if empty)')
param azureOpenAiEndpoint string = ''

@description('Azure OpenAI API key (optional)')
@secure()
param azureOpenAiKey string = ''

@description('API image tag to deploy (e.g. v1)')
param imageTag string = 'v1'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Azure region for Static Web App (must support Microsoft.Web/staticSites)')
param swaLocation string = 'eastus2'

// ── Unique suffix for globally-unique names ──────────────────────────────────
var uniqueSuffix  = uniqueString(resourceGroup().id)
var acrName       = 'commitfhlacr${take(uniqueSuffix, 6)}'
var storageName   = 'cfhlstorage${take(uniqueSuffix, 8)}'

// ── Storage Account (Table Storage — replaces Azurite in production) ─────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name:     storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion:        'TLS1_2'
    allowBlobPublicAccess:    false
    supportsHttpsTrafficOnly: true
  }
}

var storageConnString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'

// ── Container Registry ────────────────────────────────────────────────────────
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name:     acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// ── Log Analytics (required by Container Apps) ────────────────────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name:     'commit-fhl-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ── Application Insights (linked to Log Analytics) ───────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name:     'commit-fhl-insights'
  location: location
  kind:     'web'
  properties: {
    Application_Type:    'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode:       'LogAnalytics'
    DisableIpMasking:    false
  }
}

// ── Container Apps Environment ────────────────────────────────────────────────
resource caEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name:     'commitEnv'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey:  logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ── Container App (C# API) ────────────────────────────────────────────────────
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name:     'commit-api'
  location: location
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      ingress: {
        external:   true
        targetPort: 8080
        transport:  'http'
      }
      registries: [
        {
          server:            acr.properties.loginServer
          username:          acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name:  'acr-password'
          value: acr.listCredentials().passwords[0].value
        }
        {
          name:  'client-secret'
          value: clientSecret
        }
        {
          name:  'storage-conn'
          value: storageConnString
        }
        {
          name:  'openai-key'
          value: azureOpenAiKey
        }
      ]
    }
    template: {
      containers: [
        {
          name:  'commit-api'
          image: '${acr.properties.loginServer}/commit-api:${imageTag}'
          resources: {
            cpu:    json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ASPNETCORE_URLS',             value: 'http://+:8080' }
            { name: 'COMMIT_ENV',                  value: 'pilot' }
            { name: 'TENANT_ID',                   value: tenantId }
            { name: 'CLIENT_ID',                   value: clientId }
            { name: 'CLIENT_SECRET',               secretRef: 'client-secret' }
            { name: 'AZURE_STORAGE_CONN',          secretRef: 'storage-conn' }
            { name: 'AZURE_OPENAI_ENDPOINT',       value: azureOpenAiEndpoint }
            { name: 'AZURE_OPENAI_KEY',            secretRef: 'openai-key' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

// ── Static Web App (React frontend) ──────────────────────────────────────────
// Note: Microsoft.Web/staticSites not available in eastus — use swaLocation param (default: eastus2)
resource staticWebApp 'Microsoft.Web/staticSites@2022-09-01' = {
  name:     'commit-app'
  location: swaLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

// ── Flow Debug Dashboard Workbook ─────────────────────────────────────────────
var workbookContent = loadTextContent('workbook.json')

resource flowWorkbook 'Microsoft.Insights/workbooks@2022-04-01' = {
  name:     guid(resourceGroup().id, 'commit-fhl-flow-workbook')
  location: location
  kind:     'shared'
  properties: {
    displayName:    'Commit-FHL Flow Debug Dashboard'
    serializedData: workbookContent
    sourceId:       appInsights.id
    category:       'workbook'
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output acrLoginServer                  string = acr.properties.loginServer
output acrName                         string = acr.name
output containerAppUrl                 string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output staticWebAppUrl                 string = 'https://${staticWebApp.properties.defaultHostname}'
output storageAccountName              string = storageAccount.name
output appInsightsConnectionString     string = appInsights.properties.ConnectionString
output appInsightsId                   string = appInsights.id
output appInsightsName                 string = appInsights.name
output workbookId                      string = flowWorkbook.id
