targetScope = 'resourceGroup'

param location string = resourceGroup().location
param environmentName string = 'dev'

var prefix = 'opspilot-${environmentName}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-appinsights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    RetentionInDays: 90
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${prefix}-kv'
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

output keyVaultName string = keyVault.name


param sqlAdminLogin string = 'opspilotadmin'

@secure()
param sqlAdminPassword string

var sqlServerName = 'opspilot-${environmentName}-${uniqueString(resourceGroup().id)}-sql'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: 'OpsPilotDb'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name

var appServicePlanName = '${prefix}-plan'
var webAppName = 'opspilot-${environmentName}-${uniqueString(resourceGroup().id)}-api'

resource appServicePlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: 'S1'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      healthCheckPath: '/health/ready'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
    }
  }
}

output webAppName string = webApp.name
output webAppDefaultHostName string = webApp.properties.defaultHostName

@secure()
param jwtKey string

resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'OpsPilotConnectionString'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

resource jwtKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'JwtKey'
  properties: {
    value: jwtKey
  }
}

var keyVaultSecretsUserRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource webAppKeyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, keyVaultSecretsUserRoleDefinitionId)
  scope: keyVault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleDefinitionId
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}


resource webAppSettings 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: webApp
  name: 'appsettings'
  properties: {
    ConnectionStrings__OpsPilot: '@Microsoft.KeyVault(SecretUri=${sqlConnectionStringSecret.properties.secretUri})'
    Jwt__Key: '@Microsoft.KeyVault(SecretUri=${jwtKeySecret.properties.secretUri})'
    Jwt__Issuer: 'OpsPilot.Api'
    Jwt__Audience: 'OpsPilot.Client'
    APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsights.properties.ConnectionString
    Caching__UseRedis: 'false'
    Messaging__Enabled: 'false'
    AI__SemanticSearchEnabled: 'true'
    ASPNETCORE_ENVIRONMENT: 'Production'
  }
}


resource stagingSlot 'Microsoft.Web/sites/slots@2025-03-01' = {
  parent: webApp
  name: 'staging'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      healthCheckPath: '/health/ready'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
    }
  }
}

output stagingSlotName string = stagingSlot.name

resource stagingSlotKeyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, stagingSlot.id, keyVaultSecretsUserRoleDefinitionId)
  scope: keyVault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleDefinitionId
    principalId: stagingSlot.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource stagingSlotSettings 'Microsoft.Web/sites/slots/config@2025-03-01' = {
  parent: stagingSlot
  name: 'appsettings'
  properties: {
    ConnectionStrings__OpsPilot: '@Microsoft.KeyVault(SecretUri=${sqlConnectionStringSecret.properties.secretUri})'
    Jwt__Key: '@Microsoft.KeyVault(SecretUri=${jwtKeySecret.properties.secretUri})'
    Jwt__Issuer: 'OpsPilot.Api'
    Jwt__Audience: 'OpsPilot.Client'
    APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsights.properties.ConnectionString
    Caching__UseRedis: 'false'
    Messaging__Enabled: 'false'
    AI__SemanticSearchEnabled: 'true'
    ASPNETCORE_ENVIRONMENT: 'Production'
  }
}

resource webAppHttp5xxAlert 'Microsoft.Insights/metricAlerts@2026-01-01' = {
  name: '${prefix}-http5xx-alert'
  location: 'global'
  properties: {
    description: 'Alerts when the OpsPilot API returns repeated HTTP 5xx responses.'
    severity: 2
    enabled: true
    scopes: [
      webApp.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    autoMitigate: true
    targetResourceType: 'Microsoft.Web/sites'
    targetResourceRegion: location
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Http5xxThreshold'
          criterionType: 'StaticThresholdCriterion'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          skipMetricValidation: false
          dimensions: []
        }
      ]
    }
    actions: []
  }
}
