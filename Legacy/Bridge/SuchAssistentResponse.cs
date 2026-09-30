namespace TOPqwWeb_Core.Legacy.Bridge
{
    public class SuchAssistentResponse
    {
        public List<SuchAssistentCategoryResponse> Categories { get; set; } = [];
        public List<SuchAssistentLeistungResponse> Leistungen { get; set; } = [];
    }

    public class SuchAssistentCategoryResponse
    {
        public string Name { get; set; } = string.Empty;
    }

    public class SuchAssistentLeistungResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SuchAssistentKreisResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SuchAssistentErgebnisResponse
    {
        public string EIID { get; set; } = string.Empty;
        public string? STUID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Einrichtungskennzeichen { get; set; }
        public string? Standortname { get; set; }
        public string? Strasse { get; set; }
        public string? Hausnummer { get; set; }
        public string? Hausnummerzusatz { get; set; }
        public string? PLZ { get; set; }
        public string? Ort { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Internet { get; set; }
        public decimal? Entfernung { get; set; }
    }
}
