param name string
param location string
param tags object
param containers array
resource account 'Microsoft.Storage/storageAccounts@2025-01-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    publicNetworkAccess: 'Enabled'
    networkAcls: { defaultAction: 'Allow', bypass: 'None' }
  }
}
resource blob 'Microsoft.Storage/storageAccounts/blobServices@2025-01-01' = {
  parent: account
  name: 'default'
  properties: {
    deleteRetentionPolicy: { enabled: true, days: 7 }
    containerDeleteRetentionPolicy: { enabled: true, days: 7 }
  }
}
resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-01-01' = [for name in containers: {
  parent: blob
  name: name
  properties: { publicAccess: 'None' }
}]
output accountId string = account.id
output accountName string = account.name
output blobEndpoint string = account.properties.primaryEndpoints.blob
output queueEndpoint string = account.properties.primaryEndpoints.queue
output tableEndpoint string = account.properties.primaryEndpoints.table
