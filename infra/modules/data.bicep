param prefix string
param location string
param tags object
param sqlAdminObjectId string
param sqlAdminTenantId string
param sqlFirewall array
param cosmosFreeTier bool
resource sql 'Microsoft.Sql/servers@2024-05-01-preview' = {
  name: '${prefix}-sql'
  location: location
  tags: tags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Group'
      login: 'TourLab Azure SQL Provisioners'
      sid: sqlAdminObjectId
      tenantId: sqlAdminTenantId
      azureADOnlyAuthentication: true
    }
  }
}
resource firewall 'Microsoft.Sql/servers/firewallRules@2024-05-01-preview' = [for (ip,i) in sqlFirewall: {
  parent: sql
  name: 'reviewed-egress-${i}'
  properties: { startIpAddress: ip, endIpAddress: ip }
}]
resource database 'Microsoft.Sql/servers/databases@2024-05-01-preview' = {
  parent: sql
  name: 'tourlab_azure_study'
  location: location
  tags: tags
  sku: { name: 'GP_S_Gen5_1', tier: 'GeneralPurpose', family: 'Gen5', capacity: 1 }
  properties: {
    collation: 'Latin1_General_100_BIN2'
    maxSizeBytes: 34359738368
    minCapacity: json('0.5')
    autoPauseDelay: 60
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}
resource backup 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2024-05-01-preview' = {
  parent: database
  name: 'default'
  properties: { retentionDays: 7 }
}
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2025-04-15' = {
  name: '${prefix}-cosmos'
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: cosmosFreeTier
    disableLocalAuth: true
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: 'Tls12'
    locations: [ { locationName: location, failoverPriority: 0, isZoneRedundant: false } ]
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
  }
}
resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2025-04-15' = {
  parent: cosmos
  name: 'tourlab-azure-dashboard'
  properties: { resource: { id: 'tourlab-azure-dashboard' }, options: { throughput: 400 } }
}
resource bookings 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2025-04-15' = {
  parent: cosmosDb
  name: 'bookings'
  properties: {
    resource: {
      id: 'bookings'
      partitionKey: { paths: [ '/userId' ], kind: 'Hash', version: 2 }
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: [ { path: '/*' } ]
        excludedPaths: []
        compositeIndexes: [ [ { path: '/departureUtc', order: 'descending' }, { path: '/id', order: 'ascending' } ] ]
      }
    }
  }
}
output sqlHost string = sql.properties.fullyQualifiedDomainName
output cosmosName string = cosmos.name
output cosmosEndpoint string = cosmos.properties.documentEndpoint
output databaseId string = database.id
