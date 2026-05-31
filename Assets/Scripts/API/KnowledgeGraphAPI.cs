using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    public static class KnowledgeGraphAPI
    {
        public static Task<KnowledgeGraphResponse> GetGraph(int entityId)
            => APIClient.GetAsync<KnowledgeGraphResponse>($"knowledge_graph?entityId={entityId}");

        public static Task<CheckTriggersResponse> CheckTriggers(int entityId)
            => APIClient.GetAsync<CheckTriggersResponse>($"knowledge_graph/check_triggers?entityId={entityId}");

        public static Task<KnowledgeNodeUpdateResponse> CompleteNode(int entityId, string nodeId)
            => APIClient.PostAsync<KnowledgeNodeCompleteRequestDTO, KnowledgeNodeUpdateResponse>(
                "knowledge_graph/complete",
                new KnowledgeNodeCompleteRequestDTO { entity_id = entityId, node_id = nodeId });

        public static Task<KnowledgeGraphInitResponse> Initialize(int entityId)
            => APIClient.PostAsync<object, KnowledgeGraphInitResponse>(
                $"knowledge_graph/init?entityId={entityId}", null);

        public static Task<UnlockedMechanicsResponse> GetUnlocks(int entityId)
            => APIClient.GetAsync<UnlockedMechanicsResponse>($"knowledge_graph/unlocks?entityId={entityId}");

        public static Task<NodeContentResponse> GetNodeContent(int entityId, string nodeId)
            => APIClient.GetAsync<NodeContentResponse>($"knowledge_graph/node_content?entityId={entityId}&nodeId={nodeId}");
    }
}
