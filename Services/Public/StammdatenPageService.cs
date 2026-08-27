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

        public Task<(string FileName, byte[] Content)?> GetAttachmentAsync(string eiid, string attachmentId, CancellationToken cancellationToken)
        {
            return _adapter.GetAttachmentAsync(eiid, attachmentId, cancellationToken);
        }

        public Task<(string FileName, byte[] Content, string ContentType)?> GetImageAsync(string eiid, CancellationToken cancellationToken)
        {
            return _adapter.GetImageAsync(eiid, cancellationToken);
        }
    }
}
