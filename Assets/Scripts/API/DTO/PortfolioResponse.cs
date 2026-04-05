namespace Game.API.DTO
{
    [System.Serializable]
    public class PortfolioResponse
    {
        public string status;
        public EntityInfoDTO entity;
        public PortfolioLotDTO[] lots;
        public PortfolioTotalDTO[] totals;
    }

    [System.Serializable]
    public class EntityInfoDTO
    {
        public int entity_id;
        public int is_player;
        public float available_cash;
    }

    [System.Serializable]
    public class PortfolioLotDTO
    {
        public int portfolio_id;
        public int entity_id;
        public string ticker_id;
        public float shares_held;
        public string purchase_date;
        public float price;
    }

    [System.Serializable]
    public class PortfolioTotalDTO
    {
        public string ticker_id;
        public float shares_held;
    }
}
