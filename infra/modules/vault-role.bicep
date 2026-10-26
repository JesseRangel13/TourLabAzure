param vaultName string
param resourceName string
@allowed(['keys','secrets'])
param resourceType string
param principalId string
param roleId string
resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = { name: vaultName }
resource key 'Microsoft.KeyVault/vaults/keys@2024-11-01' existing = { parent: vault, name: resourceName }
resource secret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = { parent: vault, name: resourceName }
resource keyRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if(resourceType=='keys') {
  scope: key
  name: guid(key.id,principalId,roleId)
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',roleId) }
}
resource secretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if(resourceType=='secrets') {
  scope: secret
  name: guid(secret.id,principalId,roleId)
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions',roleId) }
}
