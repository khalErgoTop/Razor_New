using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Public
{
    public class StammdatenPageService
    {
        private readonly PublicStammdatenAdapter _adapter;

        public StammdatenPageService(PublicStammdatenAdapter adapter)
        {
            _adapter = adapter;
        }

        public Task<StammdatenResponse?> GetAsync(string eiid, string? stuid, CancellationToken cancellationToken)
        {
            return _adapter.GetAsync(eiid, stuid, cancellationToken);
        }

        public Task<PublicInstitutionResponse?> GetInstitutionAsync(string trid, CancellationToken cancellationToken)
        {
            return _adapter.GetInstitutionAsync(trid, cancellationToken);
        }

        public Task<(string FileName, byte[] Content)?> GetAttachmentAsync(string eiid, string attachmentId, CancellationToken cancellationToken)
        {
            return GetPublicAttachmentAsync(eiid, attachmentId, 102, cancellationToken);
        }

        public async Task<(string FileName, byte[] Content)?> GetPublicAttachmentAsync(string eiid, string attachmentId, int attachmentType, CancellationToken cancellationToken)
        {
            if (attachmentType != 102) return null;
            if (await _adapter.GetAsync(eiid, null, cancellationToken) is null) return null;
            return await _adapter.GetAttachmentAsync(eiid, attachmentId, attachmentType, cancellationToken);
        }

        public async Task<(string FileName, byte[] Content, string ContentType)?> GetImageAsync(string eiid, CancellationToken cancellationToken)
        {
            if (await _adapter.GetAsync(eiid, null, cancellationToken) is null) return null;
            return await _adapter.GetImageAsync(eiid, cancellationToken);
        }
    }
}
