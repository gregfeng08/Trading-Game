using System;

namespace Game.API.DTO
{
    [Serializable]
    public class NodePositionDTO
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class KnowledgeNodeStateDTO
    {
        public string id;
        public string title;
        public string type;
        public string description;
        public string content;
        public string[] prerequisites;
        public string category;
        public string priority;
        public NodePositionDTO position;
        public string status;
        public string unlocked_at;
        public string completed_at;
        public string reward_mechanic;
        public string trigger_explanation;
        public string correct_action;
    }

    [Serializable]
    public class CategoryDTO
    {
        public string color;
        public string label;
    }

    [Serializable]
    public class KnowledgeGraphCategoryDTO
    {
        public string color;
        public string label;
    }

    [Serializable]
    public class KnowledgeGraphResponse
    {
        public string status;
        public KnowledgeNodeStateDTO[] nodes;
    }

    [Serializable]
    public class UnlockedNodeDTO
    {
        public string node_id;
        public string title;
        public string priority;
        public string category;
    }

    [Serializable]
    public class CheckTriggersResponse
    {
        public string status;
        public UnlockedNodeDTO[] newly_unlocked;
    }

    [Serializable]
    public class KnowledgeNodeCompleteRequestDTO
    {
        public int entity_id;
        public string node_id;
    }

    [Serializable]
    public class KnowledgeNodeUpdateResponse
    {
        public string status;
        public string node_id;
        public string new_status;
        public string message;
        public string[] newly_unlocked;
        public string reward_mechanic;
    }

    [Serializable]
    public class UnlockedMechanicsResponse
    {
        public string status;
        public string[] mechanics;
    }

    [Serializable]
    public class KnowledgeGraphInitResponse
    {
        public string status;
        public string[] unlocked_nodes;
    }
}
