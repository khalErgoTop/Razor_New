namespace TOPqwWeb_Core.Services.Modules
{
    using System.Xml.Linq;
    using TOPqwWeb_Core.Legacy.Adapters;
    using TOPqwWeb_Core.Legacy.Bridge;

    public sealed class QmService
    {
        private readonly QmAdapter _adapter;

        public QmService(QmAdapter adapter)
        {
            _adapter = adapter;
        }

        public Task<bool> SaveFrageWertAsync(int QMBogenID, int QIID, string format, string value, CancellationToken cancellationToken) =>
            _adapter.SaveFrageWertAsync(QMBogenID, QIID, format, value, cancellationToken);

        public Task<bool> SetBogenInBearbeitungAsync(int QMBogenID, string EIID, CancellationToken cancellationToken) =>
            _adapter.SetBogenInBearbeitungAsync(QMBogenID, EIID, cancellationToken);

        public Task<bool> SetBogenVersandtAsync(int QMBogenID, string EIID, CancellationToken cancellationToken) =>
            _adapter.SetBogenVersandtAsync(QMBogenID, EIID, cancellationToken);

        public async Task<string?> CreateQmXmlAsync(string EIID, int QLID, int QMBogenID, CancellationToken cancellationToken)
        {
            var bogen = await _adapter.GetQmBogenAsync(EIID, QLID, cancellationToken);
            if (bogen is null || bogen.ID != QMBogenID) return null;

            var ETID = await _adapter.GetFacilityTypeAsync(EIID, cancellationToken);
            var oberkategorien = ETID.HasValue
                ? await _adapter.GetQmBogenStrukturAsync(QLID, ETID.Value, QMBogenID, cancellationToken)
                : Array.Empty<QmOberkategorieResponse>();
            var header = await _adapter.GetQmXmlHeaderAsync(EIID, QLID, cancellationToken);

            var status = bogen.Status switch
            {
                1 => "Neu",
                2 => "In_Bearbeitung",
                3 => "Versandt",
                4 => "Angenommen",
                5 => "Abgelehnt",
                6 => "Nachtrag",
                _ => bogen.Status.ToString()
            };
            var oberkategorieElements = new List<XElement>();
            foreach (var oberkategorie in oberkategorien)
            {
                var unterkategorieElements = new List<XElement>();
                foreach (var unterkategorie in oberkategorie.Unterkategorien)
                {
                    var frageElements = unterkategorie.Fragen.Select(frage => new XElement("Begriff",
                        new XElement("Name", frage.FrageKurz),
                        new XElement("ID", frage.ID),
                        new XElement("Typ", frage.Eintragstyp),
                        new XElement("Wert", new XAttribute("Typ", frage.Format ?? string.Empty), frage.FrageWert?.Wert ?? string.Empty)));
                    unterkategorieElements.Add(new XElement("Unterkategorie",
                        new XElement("Name", unterkategorie.Bezeichnung),
                        new XElement("ID", unterkategorie.ID),
                        new XElement("Begriffe", frageElements)));
                }
                oberkategorieElements.Add(new XElement("Oberkategorie",
                    new XElement("Name", oberkategorie.Bezeichnung),
                    new XElement("ID", oberkategorie.ID),
                    new XElement("Unterkategorien", unterkategorieElements)));
            }
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("Bogen",
                    new XElement("Name", bogen.Bezeichnung),
                    new XElement("Einrichtungskennzeichen", bogen.EIID),
                    new XElement("Bogenbezeichnung", bogen.Bezeichnung),
                    new XElement("Status", new XAttribute("Wert", bogen.Status), status),
                    new XElement("GeaendertAm", bogen.GeaendertAm),
                    new XElement("Layoutjahr", header?.LayoutJahr),
                    header is null ? null : CreateXmlAddress(header),
                    new XElement("Oberkategorien", oberkategorieElements)));
            return document.ToString(SaveOptions.DisableFormatting);
        }

        private static XElement CreateXmlAddress(QmXmlHeaderResponse header)
        {
            var einrichtung = new XElement("Einrichtung",
                new XElement("EinrichtungsID", header.EinrichtungsId),
                new XElement("Aktenzeichen", header.Aktenzeichen),
                new XElement("Name", header.Einrichtungsname),
                new XElement("Einrichtungskennzeichen", header.Einrichtungskennzeichen),
                new XElement("Adresse",
                    new XElement("Strasse", string.Join(" ", new[] { header.EinrichtungsStrasse, header.EinrichtungsHausnummer, header.EinrichtungsHausnummerZusatz }.Where(value => !string.IsNullOrWhiteSpace(value)))),
                    new XElement("PLZ", header.EinrichtungsPlz),
                    new XElement("Ort", header.EinrichtungsOrt),
                    new XElement("Kreis", new XAttribute("Wert", header.KreisId?.ToString() ?? string.Empty), header.Kreis)),
                new XElement("Einrichtungstypen", header.Einrichtungstypen.Select(type => new XElement("Einrichtungstyp", new XElement("EinrichtungstypBezeichnung", type.Einrichtungstyp), new XElement("EinrichtungstypKennzeichnung", type.Typkennzeichnung)))));
            var traeger = new XElement("Traeger", new XElement("TraegerID", header.TraegerId), new XElement("TraegerName", header.Traegername), new XElement("Adresse", new XElement("Strasse", header.TraegerStrasse), new XElement("PLZ", header.TraegerPlz), new XElement("Ort", header.TraegerOrt)));
            if (header.IstTraegerBogen) return traeger;
            einrichtung.Add(traeger);
            return einrichtung;
        }

        public Task<QmBogenResponse?> GetQmBogenAsync(string EIID, int QLID, CancellationToken cancellationToken)
        {
            return _adapter.GetQmBogenAsync(EIID, QLID, cancellationToken);
        }

        public Task<IReadOnlyList<QmFacilityResponse>> GetFacilitiesAvailableAsync(int iQMBogenstatus, CancellationToken cancellationToken)
        {
            return _adapter.GetFacilitiesAvailableAsync(iQMBogenstatus, cancellationToken);
        }

        public Task<IReadOnlyList<QmArchiveFacilityResponse>> GetArchiveFacilitiesAsync(CancellationToken cancellationToken)
        {
            return _adapter.GetArchiveFacilitiesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<QmSelectableBogenResponse>> GetSelectableBoegenAsync(string EIID, int ETID, int iQMBogenstatus, CancellationToken cancellationToken)
        {
            return _adapter.GetSelectableBoegenAsync(EIID, ETID, iQMBogenstatus, cancellationToken);
        }

        public Task<IReadOnlyList<QmOberkategorieResponse>> GetQmBogenStrukturAsync(int QLID, int ETID, int QMBogenID, CancellationToken cancellationToken)
        {
            return _adapter.GetQmBogenStrukturAsync(QLID, ETID, QMBogenID, cancellationToken);
        }

        public Task<int?> GetFacilityTypeAsync(string EIID, CancellationToken cancellationToken)
        {
            return _adapter.GetFacilityTypeAsync(EIID, cancellationToken);
        }

        public Task<QmFrageWertResponse?> GetFrageWertAsync(int QMBogenID, int QIID, CancellationToken cancellationToken)
        {
            return _adapter.GetFrageWertAsync(QMBogenID, QIID, cancellationToken);
        }

        public Task<IReadOnlyDictionary<int, QmFrageWertResponse>> GetFrageWerteAsync(int QMBogenID, CancellationToken cancellationToken)
        {
            return _adapter.GetFrageWerteAsync(QMBogenID, cancellationToken);
        }

        public IReadOnlyList<QmBogenStatus> GetBogenstatus()
        {
            return new[]
            {
                new QmBogenStatus(-1, "Alle Stati"),
                new QmBogenStatus(1, "Neu"),
                new QmBogenStatus(2, "In_Bearbeitung"),
                new QmBogenStatus(3, "Versandt"),
                new QmBogenStatus(4, "Angenommen"),
                new QmBogenStatus(5, "Abgelehnt"),
                new QmBogenStatus(6, "Nachtrag")
            };
        }
    }

    public sealed record QmBogenStatus(int Value, string Text);
}
