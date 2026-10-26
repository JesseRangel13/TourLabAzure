param prefix string
param location string
param tags object
resource logs 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: '${prefix}-logs'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json('0.1') }
    features: { enableLogAccessUsingOnlyResourcePermissions: true }
  }
}
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-insights'
  location: location
  tags: tags
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id, DisableLocalAuth: true }
}
output connectionString string = insights.properties.ConnectionString
output insightsId string = insights.id
