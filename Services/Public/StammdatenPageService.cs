using TOPqwWeb_Core.Infrastructure.Security;
using TOPqwWeb_Core.Legacy.Adapters;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Services.Public
{
    public class StammdatenPageService
    {
        private const string AttachmentViewPermission = "bc728ced-26b2-4eed-bb99-d90d345804d7";
        private readonly PublicStammdatenAdapter _adapter;
        private readonly CurrentUserAccessor _currentUser;
        private readonly LegacyPermissionService _permissions;

        public StammdatenPageService(PublicStammdatenAdapter adapter, CurrentUserAccessor currentUser, LegacyPermissionService permissions)
        {
            _adapter = adapter;
            _currentUser = currentUser;
            _permissions = permissions;
        }

        public async Task<StammdatenResponse?> GetAsync(string eiid, string? stuid, CancellationToken cancellationToken)
        {
            var facility = await _adapter.GetAsync(eiid, stuid, cancellationToken);
            if (facility is not null && facility.Attachments.Count > 0 && !await CanViewAttachmentsAsync(cancellationToken))
            {
                facility.Attachments.Clear();
            }
            return facility;
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
            if (!await CanViewAttachmentsAsync(cancellationToken)) return null;
            if (await _adapter.GetAsync(eiid, null, cancellationToken) is null) return null;
            return await _adapter.GetAttachmentAsync(eiid, attachmentId, attachmentType, cancellationToken);
        }

        private async Task<bool> CanViewAttachmentsAsync(CancellationToken cancellationToken)
        {
            var beid = _currentUser.BEID;
            return beid.HasValue
                && await _adapter.GetBooleanOptionAsync("Stammdaten.WithAttachments", cancellationToken)
                && await _permissions.HasPermissionAsync(beid.Value, AttachmentViewPermission, cancellationToken);
        }

        public async Task<(string FileName, byte[] Content, string ContentType)?> GetImageAsync(string eiid, CancellationToken cancellationToken)
        {
            if (await _adapter.GetAsync(eiid, null, cancellationToken) is null) return null;
            return await _adapter.GetImageAsync(eiid, cancellationToken);
        }
    }
}
