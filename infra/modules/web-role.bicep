param appName string
param principalId string
resource app 'Microsoft.Web/sites@2024-11-01' existing = { name: appName }
resource role 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: app
  name: guid(app.id,principalId,'website-contributor')
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','de139f84-1756-47ae-9be6-808fbbe84772') }
}
