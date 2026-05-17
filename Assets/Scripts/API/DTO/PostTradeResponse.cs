using UnityEngine;

namespace Game.API.DTO
{
    /// <summary>
    /// POST Request sending a trade to be async processed on Flask, status should be OK if processed properly
    /// </summary>
    [System.Serializable]
    public class PostTradeResponse
    {
        public string status;
        public string message;
        public float filled_price;
        public string order_type;
    }
}