using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    public static class ArcAPI
    {
        public static Task<ArcStatusResponse> GetStatus(string entityId)
            => APIClient.GetAsync<ArcStatusResponse>($"arc/status?entityId={entityId}");

        public static Task<ArcAdvanceResponse> Advance(string entityId)
            => APIClient.PostAsync<object, ArcAdvanceResponse>($"arc/advance?entityId={entityId}", new { });

        public static Task<ArcGradesResponse> GetGrades(string entityId)
            => APIClient.GetAsync<ArcGradesResponse>($"arc/grades?entityId={entityId}");
    }
}
