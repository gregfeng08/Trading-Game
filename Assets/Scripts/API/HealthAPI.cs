using UnityEngine;
using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    /// <summary>
    /// API for server health-related functions
    /// </summary>
    public static class HealthAPI
    {
        public static Task<PingResponse> PingAsync()
            => APIClient.GetAsync<PingResponse>("ping");

        public static Task<StatusResponse> StatusAsync()
            => APIClient.GetAsync<StatusResponse>("status");
    }
}

