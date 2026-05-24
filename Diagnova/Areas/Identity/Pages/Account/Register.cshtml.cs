using System.ComponentModel.DataAnnotations;
using Diagnova.Models;
using Diagnova.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diagnova.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MongoDbService _mongoDb;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        MongoDbService mongoDb)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _mongoDb = mongoDb;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        // Personal Identity
        [Required]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Display(Name = "Gender / biological sex")]
        public string Gender { get; set; } = string.Empty;

        [Display(Name = "Profile photo URL (optional)")]
        public string? ProfilePhotoUrl { get; set; }

        // Physical Health Metrics
        [Required]
        [Display(Name = "Current weight (kg)")]
        public double WeightKg { get; set; }

        [Required]
        [Display(Name = "Height (cm)")]
        public double HeightCm { get; set; }

        [Display(Name = "Blood type (optional)")]
        public string? BloodType { get; set; }

        // Medical Background
        [Display(Name = "Pre-existing conditions")]
        public List<string> PreExistingConditions { get; set; } = new();

        [Display(Name = "Current medications")]
        public string? CurrentMedications { get; set; }

        [Display(Name = "Known allergies")]
        public List<string> Allergies { get; set; } = new();

        [Display(Name = "Smoking habit")]
        public string SmokingHabit { get; set; } = "Never";

        [Display(Name = "Alcohol habit")]
        public string AlcoholHabit { get; set; } = "Never";

        // Health Goals
        [Display(Name = "Primary health goal")]
        public string PrimaryGoal { get; set; } = "general wellness";

        [Display(Name = "Activity level")]
        public string ActivityLevel { get; set; } = "Sedentary";

        [Display(Name = "Daily calorie target (optional)")]
        public int? DailyCalorieTarget { get; set; }

        // Emergency & Contact Info (ONLY EMAIL - NO PHONE)
        [Display(Name = "Emergency contact name")]
        public string? EmergencyContactName { get; set; }

        [EmailAddress]
        [Display(Name = "Emergency contact email")]
        public string? EmergencyContactEmail { get; set; }

        [Phone]
        [Display(Name = "Emergency contact phone (for SMS SOS)")]
        public string? EmergencyContactPhone { get; set; }

        [Display(Name = "Country / region")]
        public string? CountryRegion { get; set; }

        // Security
        [Display(Name = "Recovery method")]
        public string RecoveryMethod { get; set; } = "email";

        [Display(Name = "Security question (if selected)")]
        public string? SecurityQuestion { get; set; }

        [Display(Name = "Security answer (if selected)")]
        public string? SecurityAnswer { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = NormalizeReturnUrl(returnUrl);
    }

    // ADD THESE TWO METHODS
    public async Task<IActionResult> OnGetCheckUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new JsonResult(new { available = false });

        var existingUser = await _userManager.FindByNameAsync(username.Trim());
        return new JsonResult(new { available = existingUser == null });
    }

    public async Task<IActionResult> OnGetCheckEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return new JsonResult(new { available = false });

        var existingUser = await _userManager.FindByEmailAsync(email.Trim());
        return new JsonResult(new { available = existingUser == null });
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = NormalizeReturnUrl(returnUrl);

        if (ModelState.IsValid)
        {
            // Check if username is unique
            var existingUser = await _userManager.FindByNameAsync(Input.Username);
            if (existingUser != null)
            {
                ModelState.AddModelError("Input.Username", "Username already taken.");
                return Page();
            }

            // Create Identity user (SQL Server)
            var user = new ApplicationUser
            {
                UserName = Input.Username,
                Email = Input.Email,
                EmailConfirmed = true // Auto-confirm for demo
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                // Create medical profile in MongoDB
                var medicalProfile = new MedicalProfile
                {
                    UserId = user.Id,
                    FullName = Input.FullName,
                    Username = Input.Username,
                    Email = Input.Email,
                    DateOfBirth = Input.DateOfBirth,
                    Gender = Input.Gender,
                    ProfilePhotoUrl = Input.ProfilePhotoUrl,
                    WeightKg = Input.WeightKg,
                    HeightCm = Input.HeightCm,
                    BloodType = Input.BloodType ?? string.Empty,
                    PreExistingConditions = Input.PreExistingConditions,
                    CurrentMedications = Input.CurrentMedications ?? string.Empty,
                    Allergies = Input.Allergies,
                    SmokingHabit = Input.SmokingHabit,
                    AlcoholHabit = Input.AlcoholHabit,
                    PrimaryGoal = Input.PrimaryGoal,
                    ActivityLevel = Input.ActivityLevel,
                    DailyCalorieTarget = Input.DailyCalorieTarget,
                    EmergencyContactName = Input.EmergencyContactName ?? string.Empty,
                    EmergencyContactEmail = Input.EmergencyContactEmail ?? string.Empty,
                    EmergencyContactPhone = Input.EmergencyContactPhone ?? string.Empty,
                    CountryRegion = Input.CountryRegion ?? string.Empty,
                    RecoveryMethod = Input.RecoveryMethod,
                    SecurityQuestion = Input.SecurityQuestion,
                    SecurityAnswer = Input.SecurityAnswer
                };

                await _mongoDb.MedicalProfiles.InsertOneAsync(medicalProfile);

                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    private string NormalizeReturnUrl(string? returnUrl)
    {
        var fallback = Url.Content("~/");
        if (string.IsNullOrWhiteSpace(returnUrl))
            return fallback;

        var expanded = returnUrl.StartsWith("~/", StringComparison.Ordinal)
            ? Url.Content(returnUrl)
            : returnUrl;

        return Url.IsLocalUrl(expanded) ? expanded : fallback;
    }
}