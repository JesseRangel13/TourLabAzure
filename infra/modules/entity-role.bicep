param namespaceName string
param entity string
param principalId string
param sender bool
var parts = split(entity,'/')
var definition = subscriptionResourceId('Microsoft.Authorization/roleDefinitions',sender ? '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39' : '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')
resource bus 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = { name: namespaceName }
resource queue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' existing = { parent: bus, name: parts[1] }
resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' existing = { parent: bus, name: parts[1] }
resource sub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' existing = { parent: topic, name: length(parts)>2 ? parts[3] : 'unused' }
resource queueRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if(parts[0]=='queues') {
  scope: queue
  name: guid(queue.id,principalId,definition)
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: definition }
}
resource topicRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if(parts[0]=='topics' && length(parts)==2) {
  scope: topic
  name: guid(topic.id,principalId,definition)
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: definition }
}
resource subRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if(length(parts)==4) {
  scope: sub
  name: guid(sub.id,principalId,definition)
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: definition }
}
