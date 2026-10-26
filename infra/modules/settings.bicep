param appName string
param settings object
resource app 'Microsoft.Web/sites@2024-11-01' existing = { name: appName }
resource config 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: app
  name: 'appsettings'
  properties: settings
}
