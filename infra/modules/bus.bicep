param prefix string
param location string
param tags object
var routing = loadJsonContent('../../config/servicebus.json').UserConfig.Namespaces[0]
resource bus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: '${prefix}-bus'
  location: location
  tags: tags
  sku: { name: 'Standard', tier: 'Standard', capacity: 0 }
  properties: { disableLocalAuth: true, minimumTlsVersion: '1.2', publicNetworkAccess: 'Enabled' }
}
resource queues 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = [for q in routing.Queues: {
  parent: bus
  name: q.Name
  properties: {
    defaultMessageTimeToLive: q.Properties.DefaultMessageTimeToLive
    lockDuration: q.Properties.LockDuration
    requiresSession: false
    requiresDuplicateDetection: false
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: true
    maxSizeInMegabytes: 1024
  }
}]
resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: bus
  name: 'tourlab-azure-domain-events'
  properties: { defaultMessageTimeToLive: 'PT1H', requiresDuplicateDetection: false, maxSizeInMegabytes: 1024 }
}
resource subscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = [for s in routing.Topics[0].Subscriptions: {
  parent: topic
  name: s.Name
  properties: { defaultMessageTimeToLive: 'PT1H', lockDuration: 'PT1M', maxDeliveryCount: 5, deadLetteringOnMessageExpiration: true }
}]
resource defaultRules 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = [for (s,i) in routing.Topics[0].Subscriptions: {
  parent: subscriptions[i]
  name: '$Default'
  properties: { filterType: 'SqlFilter', sqlFilter: { sqlExpression: '1=0' } }
}]
resource filters 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = [for (s,i) in routing.Topics[0].Subscriptions: {
  parent: subscriptions[i]
  name: 'explicit-types-v1'
  properties: { filterType: 'SqlFilter', sqlFilter: { sqlExpression: s.Rules[0].Properties.SqlFilter.SqlExpression } }
}]
output busName string = bus.name
output namespace string = '${bus.name}.servicebus.windows.net'
