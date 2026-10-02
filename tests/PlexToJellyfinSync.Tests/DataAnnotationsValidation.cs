using System.ComponentModel.DataAnnotations;

namespace PlexToJellyfinSync.Tests;

/// <summary>
/// Helpers to validate data annotations in tests
/// </summary>
internal static class DataAnnotationsValidation
{
    #region Methods

    /// <summary>
    /// Validate an object the way <c>ValidateDataAnnotations</c> does
    /// </summary>
    /// <param name="instance">Object to validate</param>
    /// <returns>The validation results</returns>
    public static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(instance, new ValidationContext(instance), results, true);

        return results;
    }

    /// <summary>
    /// Assert that the given member is reported
    /// </summary>
    /// <param name="results">Validation results</param>
    /// <param name="memberName">Expected member name</param>
    public static void AssertNamesMember(List<ValidationResult> results, string memberName)
    {
        Assert.Contains(memberName, results.SelectMany(obj => obj.MemberNames).ToList(), $"The result should name {memberName}!");
    }

    #endregion // Methods
}