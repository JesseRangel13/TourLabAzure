targetScope = 'resourceGroup'
@minLength(4)
@maxLength(6)
param suffix string
param location string
param owner string
param sqlAdminObjectId string
param sqlAdminTenantId string
param sqlFirewall array
param knownProxies object
param humanPublicKeys array
param quotePublicKeys array
param humanSigningFingerprint string
param quoteSigningFingerprint string
param humanSecretVersion string
param quoteSecretVersion string
param githubRepository string
@allowed(['B2','B3'])
param apiSku string = 'B2'
param cosmosFreeTier bool = false
param budgetUsd int
param budgetEmails array
param budgetStartDate string
param configureApplications bool = false
var prefix = 'tourlabazure-${suffix}'
var tags = { Project: 'TourLabAzure', Environment: 'Study', Owner: owner, ManagedBy: 'Bicep' }
var workloads = [ 'gateway', 'catalog', 'reservations', 'payments', 'notifications', 'receipts' ]
var schemas = [ 'identity', 'catalog', 'reservations', 'payments', 'notifications' ]
module images 'modules/storage.bicep' = { name: 'images', params: { name: 'tourlabazure${suffix}img', location: location, tags: tags, containers: [ 'tourlab-azure-tour-images' ] } }
module receipts 'modules/storage.bicep' = { name: 'receipts', params: { name: 'tourlabazure${suffix}rcp', location: location, tags: tags, containers: [ 'tourlab-azure-booking-receipts' ] } }
module cursor 'modules/storage.bicep' = { name: 'cursor', params: { name: 'tourlabazure${suffix}key', location: location, tags: tags, containers: [ 'tourlab-azure-dashboard-keys' ] } }
module host 'modules/storage.bicep' = { name: 'host', params: { name: 'tourlabazure${suffix}hst', location: location, tags: tags, containers: [ 'tourlab-azure-deployments' ] } }
module data 'modules/data.bicep' = { name: 'data', params: { prefix: prefix, location: location, tags: tags, sqlAdminObjectId: sqlAdminObjectId, sqlAdminTenantId: sqlAdminTenantId, sqlFirewall: sqlFirewall, cosmosFreeTier: cosmosFreeTier } }
module bus 'modules/bus.bicep' = { name: 'bus', params: { prefix: prefix, location: location, tags: tags } }
module vault 'modules/vault.bicep' = { name: 'vault', params: { prefix: prefix, location: location, tags: tags, tenantId: sqlAdminTenantId } }
module monitor 'modules/monitoring.bicep' = { name: 'monitor', params: { prefix: prefix, location: location, tags: tags } }
module hosting 'modules/hosting.bicep' = { name: 'hosting', params: { prefix: prefix, location: location, tags: tags, hostStorageName: host.outputs.accountName, hostBlobEndpoint: host.outputs.blobEndpoint, apiSku: apiSku } }
var sqlConnection = 'Server=tcp:${data.outputs.sqlHost},1433;Database=tourlab_azure_study;Authentication=Active Directory Managed Identity;Encrypt=Strict;TrustServerCertificate=False;Max Pool Size=5;Connect Timeout=5'
var common = {
  AZURELAB_AzureSecurity__Mode: 'ManagedIdentity'
  AZURELAB_ConnectionStrings__Runtime: sqlConnection
  AZURELAB_LabFeaturesEnabled: 'false'
  ASPNETCORE_ENVIRONMENT: 'Production'
  DOTNET_ENVIRONMENT: 'Production'
  ASPNETCORE_FORWARDEDHEADERS_ENABLED: 'false'
}
var configs = [
  {
    AZURELAB_KeyVault__VaultUri: vault.outputs.vaultUri
    AZURELAB_KeyVault__SigningSecretIdentifier: '${vault.outputs.vaultUri}secrets/tourlab-azure-human-signing/${humanSecretVersion}'
    AZURELAB_KeyVault__ExpectedSigningKeyId: humanSigningFingerprint
    AZURELAB_DataProtection__KeyIdentifier: '${vault.outputs.vaultUri}keys/tourlab-azure-gateway-protection'
    AZURELAB_Browser__AllowedOrigins__0: 'https://${hosting.outputs.appHosts[0]}'
    AZURELAB_Backends__catalog: 'https://${hosting.outputs.appHosts[1]}/'
    AZURELAB_Backends__reservations: 'https://${hosting.outputs.appHosts[2]}/'
    AZURELAB_Backends__payments: 'https://${hosting.outputs.appHosts[3]}/'
    AZURELAB_Backends__notifications: 'https://${hosting.outputs.appHosts[4]}/'
  }
  {
    AZURELAB_ImageStorage__Enabled: 'true'
    AZURELAB_ImageStorage__Local: 'false'
    AZURELAB_ImageStorage__ServiceUri: images.outputs.blobEndpoint
    AZURELAB_ImageStorage__Container: 'tourlab-azure-tour-images'
  }
  {
    AZURELAB_KeyVault__VaultUri: vault.outputs.vaultUri
    AZURELAB_KeyVault__SigningSecretIdentifier: '${vault.outputs.vaultUri}secrets/tourlab-azure-quote-signing/${quoteSecretVersion}'
    AZURELAB_KeyVault__ExpectedSigningKeyId: quoteSigningFingerprint
    AZURELAB_Catalog__BaseAddress: 'https://${hosting.outputs.appHosts[1]}/'
    AZURELAB_Dashboard__Enabled: 'true'
    AZURELAB_Dashboard__Local: 'false'
    AZURELAB_Dashboard__Endpoint: data.outputs.cosmosEndpoint
    AZURELAB_Dashboard__Database: 'tourlab-azure-dashboard'
    AZURELAB_Dashboard__Container: 'bookings'
    AZURELAB_Dashboard__KeyBlobUri: '${cursor.outputs.blobEndpoint}tourlab-azure-dashboard-keys/cursor-ring.xml'
    AZURELAB_Dashboard__ProtectionKeyIdentifier: '${vault.outputs.vaultUri}keys/tourlab-azure-dashboard-protection'
  }
  {}
  {
    AZURELAB_ReceiptStorage__Enabled: 'true'
    AZURELAB_ReceiptStorage__Local: 'false'
    AZURELAB_ReceiptStorage__ServiceUri: receipts.outputs.blobEndpoint
    AZURELAB_ReceiptStorage__Container: 'tourlab-azure-booking-receipts'
  }
]
module settings 'modules/settings.bicep' = [for (w,i) in take(workloads,5): if(configureApplications) {
  name: 'settings-${w}'
  params: {
    appName: hosting.outputs.appNames[i]
    settings: union(common, configs[i], {
      AZURELAB_AllowedHosts: hosting.outputs.appHosts[i]
      SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
    }, toObject(range(0,length(knownProxies[w])), j => 'AZURELAB_Ingress__KnownProxies__${j}', j => knownProxies[w][j]),
    i>0 ? union({
      AZURELAB_Messaging__Enabled: 'true'
      AZURELAB_Messaging__FullyQualifiedNamespace: bus.outputs.namespace
    }, toObject(range(0,length(humanPublicKeys)),j => 'AZURELAB_Jwt__ValidationKeys__${j}',j => humanPublicKeys[j])) : {},
    i==1 ? toObject(range(0,length(quotePublicKeys)),j => 'AZURELAB_Quote__ValidationKeys__${j}',j => quotePublicKeys[j]) : {})
  }
}]
module functionSettings 'modules/settings.bicep' = if(configureApplications) {
  name: 'settings-receipts'
  params: {
    appName: hosting.outputs.functionName
    settings: union(common,configs[4], {
      AZURELAB_ReceiptBus__fullyQualifiedNamespace: bus.outputs.namespace
      AZURELAB_ReceiptBus__credential: 'managedidentity'
      ReceiptBus__fullyQualifiedNamespace: bus.outputs.namespace
      ReceiptBus__credential: 'managedidentity'
      AzureWebJobsStorage__credential: 'managedidentity'
      AzureWebJobsStorage__blobServiceUri: host.outputs.blobEndpoint
      AzureWebJobsStorage__queueServiceUri: host.outputs.queueEndpoint
      AzureWebJobsStorage__tableServiceUri: host.outputs.tableEndpoint
      APPLICATIONINSIGHTS_CONNECTION_STRING: monitor.outputs.connectionString
      APPLICATIONINSIGHTS_AUTHENTICATION_STRING: 'Authorization=AAD'
    })
  }
}
var permissions = loadJsonContent('../config/cloud/permissions.json').workloads
var receives = flatten(map(workloads,(w,i) => map(permissions[w].?serviceBusReceive ?? [],e => { workload: i, entity: e })))
var sends = flatten(map(workloads,(w,i) => map(permissions[w].?serviceBusSend ?? [],e => { workload: i, entity: e })))
var principalIds = concat(hosting.outputs.principals,[hosting.outputs.functionPrincipal])
module receiveRoles 'modules/entity-role.bicep' = [for (p,i) in receives: {
  name: 'receive-${i}'
  params: { namespaceName: bus.outputs.busName, entity: p.entity, principalId: principalIds[p.workload], sender: false }
}]
module sendRoles 'modules/entity-role.bicep' = [for (p,i) in sends: {
  name: 'send-${i}'
  params: { namespaceName: bus.outputs.busName, entity: p.entity, principalId: principalIds[p.workload], sender: true }
}]
resource readWriteRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id,'tourlab-azure-blob-read-write')
  properties: {
    roleName: 'TourLab Azure Blob Read Write ${suffix}'
    description: 'Container-assigned read/write only. No delete, key listing or management privileges.'
    type: 'CustomRole'
    assignableScopes: [ resourceGroup().id ]
    permissions: [ { actions: [], notActions: [], dataActions: [ 'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read', 'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/write' ], notDataActions: [] } ]
  }
}
var blobGrants = [
  { account: images.outputs.accountName, container: 'tourlab-azure-tour-images', principal: principalIds[1], role: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','ba92f5b4-2d11-453d-a403-e96b0029c9fe') }
  { account: cursor.outputs.accountName, container: 'tourlab-azure-dashboard-keys', principal: principalIds[2], role: readWriteRole.id }
  { account: receipts.outputs.accountName, container: 'tourlab-azure-booking-receipts', principal: principalIds[4], role: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','2a2b9908-6ea1-4ae2-8e65-a410df84e7d1') }
  { account: receipts.outputs.accountName, container: 'tourlab-azure-booking-receipts', principal: principalIds[5], role: readWriteRole.id }
]
module blobRoles 'modules/blob-role.bicep' = [for i in range(0,4): {
  name: 'blob-${i}'
  params: { accountName: blobGrants[i].account, containerName: blobGrants[i].container, principalId: blobGrants[i].principal, roleId: blobGrants[i].role }
}]
var vaultGrants = [
  { kind: 'secrets', name: 'tourlab-azure-human-signing', principal: principalIds[0], role: '4633458b-17de-408a-b874-0445c86b69e6' }
  { kind: 'secrets', name: 'tourlab-azure-quote-signing', principal: principalIds[2], role: '4633458b-17de-408a-b874-0445c86b69e6' }
  { kind: 'keys', name: 'tourlab-azure-gateway-protection', principal: principalIds[0], role: 'e147488a-f6f5-4113-8e2d-b22465e65bf6' }
  { kind: 'keys', name: 'tourlab-azure-dashboard-protection', principal: principalIds[2], role: 'e147488a-f6f5-4113-8e2d-b22465e65bf6' }
]
module vaultRoles 'modules/vault-role.bicep' = [for i in range(0,4): if(configureApplications || i>=2) {
  name: 'vault-role-${i}'
  params: { vaultName: vault.outputs.vaultName, resourceType: vaultGrants[i].kind, resourceName: vaultGrants[i].name, principalId: vaultGrants[i].principal, roleId: vaultGrants[i].role }
}]
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2025-04-15' existing = { name: '${prefix}-cosmos' }
resource cosmosRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2025-04-15' = {
  parent: cosmos
  name: guid(cosmos.id,prefix,'reservations-bookings-contributor')
  properties: {
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: principalIds[2]
    scope: '${cosmos.id}/dbs/tourlab-azure-dashboard/colls/bookings'
  }
  dependsOn: [ data ]
}
resource migrators 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = [for s in schemas: {
  name: '${prefix}-migrate-${s}'
  location: location
  tags: tags
}]
resource federations 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = [for (s,i) in schemas: {
  parent: migrators[i]
  name: 'github-environment'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepository}:environment:azure-study-migrate-${s}'
    audiences: [ 'api://AzureADTokenExchange' ]
  }
}]
resource insights 'Microsoft.Insights/components@2020-02-02' existing = { name: '${prefix}-insights' }
resource metricRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: insights
  name: guid(insights.id,prefix,'receipts-metrics')
  properties: { principalId: principalIds[5], principalType: 'ServicePrincipal', roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','3913510d-42f4-4e42-8a64-420c390055eb') }
  dependsOn: [ monitor ]
}
resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: '${prefix}-study-budget'
  properties: {
    category: 'Cost'
    amount: budgetUsd
    timeGrain: 'Monthly'
    timePeriod: { startDate: budgetStartDate }
    notifications: {
      actual80: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 80, thresholdType: 'Actual', contactEmails: budgetEmails }
      forecast100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Forecasted', contactEmails: budgetEmails }
    }
  }
}
output workloadPrincipals object = toObject(range(0,6),i => workloads[i],i => principalIds[i])
output migratorPrincipals object = {
  identity: migrators[0].properties.principalId
  catalog: migrators[1].properties.principalId
  reservations: migrators[2].properties.principalId
  payments: migrators[3].properties.principalId
  notifications: migrators[4].properties.principalId
}
output migratorClientIds object = {
  identity: migrators[0].properties.clientId
  catalog: migrators[1].properties.clientId
  reservations: migrators[2].properties.clientId
  payments: migrators[3].properties.clientId
  notifications: migrators[4].properties.clientId
}
output apiHosts array = hosting.outputs.appHosts
output functionAppName string = hosting.outputs.functionName
output sqlDatabaseId string = data.outputs.databaseId


resource delivery 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${prefix}-deliver'
  location: location
  tags: tags
}
resource deliveryFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: delivery
  name: 'github-environment'
  properties: { issuer: 'https://token.actions.githubusercontent.com', subject: 'repo:${githubRepository}:environment:azure-study-deploy', audiences: [ 'api://AzureADTokenExchange' ] }
}
module deliveryRoles 'modules/web-role.bicep' = [for (w,i) in workloads: {
  name: 'deliver-${w}'
  params: { appName: i==5 ? hosting.outputs.functionName : hosting.outputs.appNames[i], principalId: delivery.properties.principalId }
}]
resource migrationReaders 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (s,i) in schemas: {
  name: guid(resourceGroup().id,migrators[i].id,'read-only-metadata')
  properties: { principalId: migrators[i].properties.principalId, principalType: 'ServicePrincipal', roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','acdd72a7-3385-48ef-bd42-f606fba81ae7') }
}]
resource deliveryReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id,delivery.id,'read-only-metadata')
  properties: { principalId: delivery.properties.principalId, principalType: 'ServicePrincipal', roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','acdd72a7-3385-48ef-bd42-f606fba81ae7') }
}
output deliveryClientId string = delivery.properties.clientId
