using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TOPqwWeb_Core.Services.Administration;

namespace TOPqwWeb_Core.Pages.Auth
{
    [Authorize(Policy = "AuthenticatedUser")]
    public class BenutzerManagementModel : PageModel
    {
        private readonly BenutzerManagementService _service;

        public BenutzerManagementModel(BenutzerManagementService service) => _service = service;

        public UserContext Context { get; private set; } = new(0, 0, false, false, []);
        public IReadOnlyList<UserListItem> Users { get; private set; } = [];
        public UserDetails? SelectedUser { get; private set; }
        public IReadOnlyList<ModuleOption> Modules { get; private set; } = [];
        public UserAssignments Assignments { get; private set; } = new([], [], [], false, false);
        public bool CanCreateUsers { get; private set; }
        public string? InfoMessage { get; private set; }

        [BindProperty(SupportsGet = true)] public int UserType { get; set; }
        [BindProperty(SupportsGet = true)] public int? SelectedUserId { get; set; }
        [BindProperty(SupportsGet = true)] public string? Constraint { get; set; }

        [BindProperty] public UserInput Input { get; set; } = new();
        [BindProperty] public string[] Facilities { get; set; } = [];
        [BindProperty] public int[] Districts { get; set; } = [];
        [BindProperty] public int[] FacilityTypes { get; set; } = [];
        [BindProperty] public NewUserInput NewUser { get; set; } = new(string.Empty, string.Empty, string.Empty, string.Empty, 0);

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
            await LoadAsync(cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteUserAsync(CancellationToken cancellationToken)
        {
            if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
            if (SelectedUserId is not int userId || userId <= 0) return BadRequest();

            var result = await _service.DeleteUserAsync(userId, cancellationToken);
            InfoMessage = result.Message;
            SelectedUserId = null;
            await LoadAsync(cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostCreateUserAsync(CancellationToken cancellationToken)
        {
            if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
            var result = await _service.CreateUserAsync(NewUser, cancellationToken);
            if (!result.Success)
            {
                InfoMessage = result.Message;
                await LoadAsync(cancellationToken);
                return Page();
            }

            InfoMessage = result.Message;
            UserType = NewUser.UserType;
            SelectedUserId = result.UserId;
            Constraint = null;
            await LoadAsync(cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostSelectTypeAsync(CancellationToken cancellationToken)
        {
            if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
            SelectedUserId = null;
            return RedirectToPage(new { UserType, Constraint });
        }

        public async Task<IActionResult> OnPostSelectUserAsync(CancellationToken cancellationToken)
        {
            if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
            await LoadAsync(cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
        {
            if (!await _service.CanAccessAsync(cancellationToken)) return Forbid();
            if (SelectedUserId is not int userId || userId <= 0) return BadRequest();
            var current = await _service.GetUserDetailsAsync(userId, cancellationToken);
            if (current is null) return NotFound();
            var hasAdminRights = await _service.HasAdminRightsAsync(cancellationToken);
            var updated = current with
            {
                Firstname = Input.Firstname?.Trim() ?? string.Empty,
                Lastname = Input.Lastname?.Trim() ?? string.Empty,
                Email = Input.Email?.Trim() ?? string.Empty,
                IsAdmin = hasAdminRights && Input.IsAdmin,
                IsKosoz = hasAdminRights && Input.IsKosoz,
                IsDeactivated = userId != (await _service.GetCurrentUserContextAsync(cancellationToken)).Beid && Input.IsDeactivated
            };
            await _service.SaveUserAsync(updated, cancellationToken);
            if (Assignments.CanEditFacilities || Assignments.CanEditDistrictsAndTypes)
                await _service.SaveAssignmentsAsync(userId, Facilities, Districts, FacilityTypes, cancellationToken);
            InfoMessage = $"Benutzer '{current.Username}' gespeichert.";
            await LoadAsync(cancellationToken);
            return Page();
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            Context = await _service.GetCurrentUserContextAsync(cancellationToken);
            CanCreateUsers = await _service.CanCreateUsersAsync(cancellationToken);
            if (UserType == 0) UserType = Context.UserTypes.FirstOrDefault()?.Value ?? 0;
            Users = UserType == 0 ? [] : await _service.GetUsersAsync(UserType, Constraint, cancellationToken);
            if (SelectedUserId is null) SelectedUserId = Users.FirstOrDefault()?.Beid;
            if (SelectedUserId is int userId)
            {
                SelectedUser = await _service.GetUserDetailsAsync(userId, cancellationToken);
                Modules = await _service.GetModulesAsync(userId, cancellationToken);
                Assignments = await _service.GetAssignmentsAsync(SelectedUser, cancellationToken);
                if (SelectedUser is not null && string.IsNullOrEmpty(Input.Username))
                    Input = UserInput.From(SelectedUser);
            }
        }

        public sealed class UserInput
        {
            public string Username { get; set; } = string.Empty;
            public string Firstname { get; set; } = string.Empty;
            public string Lastname { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public bool IsAdmin { get; set; }
            public bool IsKosoz { get; set; }
            public bool IsDeactivated { get; set; }

            public static UserInput From(UserDetails user) => new()
            {
                Username = user.Username, Firstname = user.Firstname, Lastname = user.Lastname,
                Email = user.Email, IsAdmin = user.IsAdmin, IsKosoz = user.IsKosoz,
                IsDeactivated = user.IsDeactivated
            };
        }
    }
}
