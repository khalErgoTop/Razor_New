namespace TOPqwWeb_Core.Services.Modules
{
    using TOPqwWeb_Core.Legacy.Adapters;
    using TOPqwWeb_Core.Legacy.Bridge;

    public sealed class QmService
    {
        private readonly QmAdapter _adapter;

        public QmService(QmAdapter adapter)
        {
            _adapter = adapter;
        }

        public Task<QmBogenResponse?> GetQmBogenAsync(string EIID, int QLID, CancellationToken cancellationToken)
        {
            return _adapter.GetQmBogenAsync(EIID, QLID, cancellationToken);
        }

        public Task<IReadOnlyList<QmFacilityResponse>> GetFacilitiesAvailableAsync(int iQMBogenstatus, CancellationToken cancellationToken)
        {
            return _adapter.GetFacilitiesAvailableAsync(iQMBogenstatus, cancellationToken);
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
