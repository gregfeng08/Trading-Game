namespace Game.API.DTO
{
    [System.Serializable]
    public class EntityRegistrationResponse
    {
        /// <summary>
        /// "ok" or "error"
        /// </summary>
        public string status;

        /// <summary>
        /// Human-readable message (optional but very useful)
        /// </summary>
        public string message;

        /// <summary>
        /// Database primary key for the entity
        /// </summary>
        public int entity_db_id;

        /// <summary>
        /// True if the entity already existed, false if newly created
        /// </summary>
        public bool already_exists;
    }
}
