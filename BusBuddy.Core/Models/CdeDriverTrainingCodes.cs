namespace BusBuddy.Core.Models;

/// <summary>
/// Colorado CDE School Finance &amp; Operations — 2024-25 License/Training Matrix requirement codes.
/// Source: https://resources.finalsite.net/images/v1764086158/cdestatecous/mpcomjjt3zryb1vussig/2024-25-License-Training-Matrix.pdf
/// </summary>
public static class CdeDriverTrainingCodes
{
    public const string BackgroundCheck = "BACKGROUND_CHECK";
    public const string MvrAnnual = "MVR_ANNUAL";
    public const string PreEmploymentDrugAlcohol = "PRE_EMPLOY_DA";
    public const string FmcsaClearinghouse = "FMCSA_CLEARINGHOUSE";
    public const string SubstanceAbuseTraining = "DA_SUBSTANCE_ABUSE_TRAINING";
    public const string FmcsaRandomDa = "FMCSA_RANDOM_DA";
    public const string MedicalForm = "MEDICAL_FORM";
    public const string PerformanceEvalPreTrip = "PERF_EVAL_PRETRIP";
    public const string SignedJobDescription = "SIGNED_JOB_DESCRIPTION";
    public const string CdeAnnualWrittenTest = "CDE_ANNUAL_WRITTEN_TEST";
    public const string CdeGuideCertificate = "CDE_GUIDE_CERTIFICATE";
    public const string ConfidentialityTraining = "CONFIDENTIALITY_TRAINING";
    public const string MountainAdverseWeather = "MOUNTAIN_ADVERSE_WEATHER";
    public const string MandatoryReporting = "MANDATORY_REPORTING";
    public const string FirstAidCpr = "FIRST_AID_CPR";
    public const string AnnualSixHourInService = "ANNUAL_SIX_HOUR_INSERVICE";
    public const string EldtPreservice = "ELDT_PRESERVICE";
    public const string CsrsTraining = "CSRS_TRAINING";
    public const string WheelchairSecurement = "WHEELCHAIR_SECUREMENT";
    public const string SpecialNeedsTraining = "SPECIAL_NEEDS_1CCR_301-26";

    public static IReadOnlyList<(string Code, string DisplayName, int? DefaultValidityMonths, bool OftenApplicable)> Catalog { get; } =
    [
        (BackgroundCheck, "Required Background Check", 24, true),
        (MvrAnnual, "MVR Pre-Employment and Annually", 12, true),
        (PreEmploymentDrugAlcohol, "Pre-Employment D & A Testing", null, true),
        (FmcsaClearinghouse, "FMCSA D & A Clearinghouse", null, true),
        (SubstanceAbuseTraining, "D & A Substance Abuse Training", null, true),
        (FmcsaRandomDa, "FMCSA D & A Random Testing", 12, true),
        (MedicalForm, "Required Medical Form (USDOT Physical / STU-17)", 24, true),
        (PerformanceEvalPreTrip, "Performance Evaluation & Pre-Trip", 12, true),
        (SignedJobDescription, "Signed Job Description", null, true),
        (CdeAnnualWrittenTest, "CDE Annual Written Test", 12, true),
        (CdeGuideCertificate, "CDE Guide Certificate of Receipt", 12, true),
        (ConfidentialityTraining, "Confidentiality Training", null, true),
        (MountainAdverseWeather, "Mountain & Adverse Weather Training", null, true),
        (MandatoryReporting, "Mandatory Reporting Training", null, true),
        (FirstAidCpr, "First Aid / CPR / Universal Precautions", 24, true),
        (AnnualSixHourInService, "Annual Six Hour In-Service", 12, true),
        (EldtPreservice, "ELDT Pre-service (Syllabus / BTW / Theory)", null, true),
        (CsrsTraining, "Proper Use and Maintenance of CSRS", null, false),
        (WheelchairSecurement, "Proper Wheelchair Securement Training", null, false),
        (SpecialNeedsTraining, "Special Needs Training (1 CCR 301-26, 5.6)", null, false)
    ];
}

/// <summary>Route vs activity duty used with the CDE license/training matrix.</summary>
public static class DriverDutyCategories
{
    public const string Route = "Route";
    public const string Activity = "Activity";
}

/// <summary>Vehicle capacity / GVWR bucket aligned to CDE matrix columns.</summary>
public static class DriverVehicleCategories
{
    public const string Route16PlusGvwrOver26001 = "Route_16+_GVWR_GT_26001";
    public const string Route16PlusGvwrUnder26001 = "Route_16+_GVWR_LT_26001";
    public const string RouteTypeA15OrLess = "Route_TypeA_15_or_less";
    public const string ActivityMf16PlusGvwrOver26001 = "Activity_MF_16+_GVWR_GT_26001";
    public const string ActivityMf16PlusGvwrUnder26001 = "Activity_MF_16+_GVWR_LT_26001";
    public const string ActivityTypeA15OrLess = "Activity_TypeA_MF_15_or_less";
    public const string ActivityUnder12 = "Activity_LT_12_passengers";
    public const string ActivityMotorcoach = "Activity_Motorcoach_16+_GVWR_GT_26001";
}
