namespace Game.API.DTO
{
    [System.Serializable]
    public class EntityDTO
    {
        /// <summary>
        /// Stable external identifier (chosen by Unity)
        /// Example: "player_001", "npc_broker_12"
        /// </summary>
        public string entity_id;

        /// <summary>
        /// Logical category
        /// Example: "player", "npc", "system"
        /// </summary>
        public string entity_type;

        /// <summary>
        /// Human-readable name for UI/debugging
        /// </summary>
        public string display_name;
    }
}
