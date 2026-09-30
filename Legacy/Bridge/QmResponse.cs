namespace TOPqwWeb_Core.Legacy.Bridge;

public sealed record QmBogenResponse(
    int ID,
    string EIID,
    int QLID,
    string Bezeichnung,
    int Status,
    DateTime? GeaendertAm);

public sealed record QmXmlHeaderResponse(
    int LayoutJahr,
    bool IstTraegerBogen,
    string EinrichtungsId,
    string Einrichtungsname,
    string Einrichtungskennzeichen,
    string? Aktenzeichen,
    string? EinrichtungsStrasse,
    string? EinrichtungsHausnummer,
    string? EinrichtungsHausnummerZusatz,
    string? EinrichtungsPlz,
    string? EinrichtungsOrt,
    string? TraegerId,
    string? Traegername,
    string? TraegerStrasse,
    string? TraegerPlz,
    string? TraegerOrt,
    IReadOnlyList<QmXmlFacilityTypeResponse> Einrichtungstypen,
    int? KreisId,
    string? Kreis);

public sealed record QmXmlFacilityTypeResponse(string Typkennzeichnung, string Einrichtungstyp);

public sealed record QmFrageResponse(
    int ID,
    int Position,
    string FrageKurz,
    string? FrageLang,
    string? Hilfetext,
    string? Format,
    bool Mussfeld,
    int Eintragstyp,
    QmFrageWertResponse? FrageWert = null);

public sealed record QmFrageWertResponse(
    int ID,
    int FKQBID,
    int FKQIID,
    int WertINT,
    double WertREAL,
    bool WertBOOL,
    string WertTXT,
    string WertMEMO)
{
    public string Wert => !string.IsNullOrWhiteSpace(WertMEMO) ? WertMEMO
        : !string.IsNullOrWhiteSpace(WertTXT) ? WertTXT
        : WertBOOL ? "Ja"
        : WertREAL != 0 ? WertREAL.ToString(System.Globalization.CultureInfo.CurrentCulture)
        : WertINT.ToString(System.Globalization.CultureInfo.CurrentCulture);
}

public sealed record QmUnterkategorieResponse(
    int ID,
    int Position,
    string Bezeichnung,
    string? BezeichnungKurz,
    IReadOnlyList<QmFrageResponse> Fragen);

public sealed record QmOberkategorieResponse(
    int ID,
    int Position,
    string Bezeichnung,
    string? BezeichnungKurz,
    IReadOnlyList<QmUnterkategorieResponse> Unterkategorien);

public sealed record QmFacilityResponse(
    string EIID,
    string Einrichtungsname,
    string Einrichtungskennzeichen,
    string? Aktenzeichen,
    string? FKTRID,
    int Status,
    int? FKETID,
    int? FKKRID,
    DateTime? GeschlossenAm,
    DateTime? GeoeffnetSeit,
    int? Platzzahl,
    string? EinrichtungsBeschreibung,
    string? GeaendertVon,
    DateTime? GeaendertAm,
    DateTime? SyncAm,
    bool ElektronischerLeistungsnachweis);

public sealed record QmSelectableBogenResponse(
    int ID,
    string EIID,
    int QLID,
    string LayoutBezeichnung,
    int LayoutJahr,
    string Bezeichnung,
    int Status,
    DateTime? GeaendertAm);

public sealed record QmArchiveFacilityResponse(
    string EIID,
    string Einrichtungsname,
    string Einrichtungskennzeichen,
    IReadOnlyList<QmArchiveBogenResponse> Boegen);

public sealed record QmArchiveBogenResponse(
    int ID,
    int QLID,
    string Bezeichnung,
    int Status,
    int LayoutJahr,
    DateTime? GeaendertAm);
