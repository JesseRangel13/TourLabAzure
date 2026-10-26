param accountName string
param containerName string
param principalId string
param roleId string
resource account 'Microsoft.Storage/storageAccounts@2025-01-01' existing = { name: accountName }
resource blob 'Microsoft.Storage/storageAccounts/blobServices@2025-01-01' existing = { parent: account, name: 'default' }
resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-01-01' existing = { parent: blob, name: containerName }
resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: container
  name: guid(container.id,principalId,roleId)
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId }
}
