using System.Threading.Tasks;

namespace Game.API
{
    public static class DialogueAPI
    {
        public static Task<DTO.DialogueResponse> GetDialogue(string date = null)
        {
            string path = string.IsNullOrEmpty(date)
                ? "/dialogue"
                : $"/dialogue?date={date}";
            return APIClient.GetAsync<DTO.DialogueResponse>(path);
        }

        public static Task<DTO.DialogueGenerateResponse> Generate(string entityId)
        {
            string path = string.IsNullOrEmpty(entityId)
                ? "/dialogue/generate"
                : $"/dialogue/generate?entityId={entityId}";
            return APIClient.PostAsync<DTO.DialogueGenerateRequestDTO, DTO.DialogueGenerateResponse>(
                path, new DTO.DialogueGenerateRequestDTO { entity_id = entityId });
        }
    }
}
