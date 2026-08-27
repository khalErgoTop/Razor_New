namespace TOPqwWeb_Core.Legacy.Bridge;

public sealed record StammdatenKostenTimeFrame(
    int HBGWZID,
    DateTime? GueltigAb,
    DateTime? GueltigBis,
    string VonBis);

public sealed record StammdatenKostenRow(
    int HBGID,
    string Bezeichnung,
    int? HBGWWID,
    decimal? MP,
    decimal? GP,
    decimal? IB,
    decimal? FB,
    DateTime? VereinbartAm,
    DateTime? UnterschriebenAm);
