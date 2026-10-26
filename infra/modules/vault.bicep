param prefix string
param location string
param tags object
param tenantId string
resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: '${prefix}-kv'
  location: location
  tags: tags
  properties: {
    tenantId: tenantId
    sku: { name: 'standard', family: 'A' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
    networkAcls: { defaultAction: 'Allow', bypass: 'None' }
  }
}
resource keys 'Microsoft.KeyVault/vaults/keys@2024-11-01' = [for name in [ 'tourlab-azure-gateway-protection', 'tourlab-azure-dashboard-protection' ]: {
  parent: vault
  name: name
  properties: { kty: 'RSA', keySize: 3072, keyOps: [ 'wrapKey', 'unwrapKey' ], attributes: { enabled: true } }
}]
output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri
