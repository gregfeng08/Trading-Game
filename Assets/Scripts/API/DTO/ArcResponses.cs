namespace Game.API.DTO
{
    [System.Serializable]
    public class ArcDefinitionDTO
    {
        public string id;
        public string name;
        public string description;
        public string start_date;
        public string end_date;
        public string newspaper_tone;
    }

    [System.Serializable]
    public class ArcStatusResponse
    {
        public string status;
        public string current_date;
        public ArcDefinitionDTO arc;
        public int trading_days_remaining;
        public double current_return_pct;
        public string projected_grade;
        public string message;
    }

    [System.Serializable]
    public class ArcGradeDTO
    {
        public string arc_id;
        public string arc_name;
        public string start_date;
        public string end_date;
        public double start_value;
        public double end_value;
        public double return_pct;
        public string grade;
        public double cash_multiplier;
    }

    [System.Serializable]
    public class ArcTransitionDTO
    {
        public ArcGradeDTO completed_arc;
        public ArcDefinitionDTO next_arc;
        public double cash_before;
        public double cash_after;
    }

    [System.Serializable]
    public class ArcAdvanceResponse
    {
        public string status;
        public ArcTransitionDTO transition;
        public string message;
    }

    [System.Serializable]
    public class ArcGradesResponse
    {
        public string status;
        public ArcGradeDTO[] grades;
    }
}
