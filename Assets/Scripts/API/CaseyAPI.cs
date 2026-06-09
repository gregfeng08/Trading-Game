using System.Threading.Tasks;

namespace Game.API
{
    public static class CaseyAPI
    {
        [System.Serializable]
        public class CaseyCommentResponse
        {
            public string comment;
        }

        public static Task<CaseyCommentResponse> GetDailyComment(string entityId)
        {
            return APIClient.GetAsync<CaseyCommentResponse>($"/casey_comment?entityId={entityId}");
        }
    }
}
