using Inventory.Domain.Common;
using Xunit;

namespace Inventory.UnitTests;

/// <summary>Task 5.15 (docs/31 §4.2) - the normalization rules the Phase 5 items search/
/// uniqueness key depends on, isolated from any database.</summary>
public sealed class ArabicTextNormalizerTests
{
    [Theory]
    [InlineData("جُبْنَة", "جبنه")] // harakat stripped, ة -> ه
    [InlineData("مُحَمَّص", "محمص")] // shadda + fatha stripped
    [InlineData("لَحْم", "لحم")]
    public void Diacritics_Are_Stripped(string input, string expected) =>
        Assert.Equal(expected, ArabicTextNormalizer.Normalize(input));

    [Theory]
    [InlineData("آجل", "اجل")]
    [InlineData("أرز", "ارز")]
    [InlineData("إمارات", "امارات")]
    public void Alef_Forms_Are_Unified_To_Plain_Alef(string input, string expected) =>
        Assert.Equal(expected, ArabicTextNormalizer.Normalize(input));

    [Theory]
    [InlineData("جبنة", "جبنه")]
    [InlineData("مسقعة", "مسقعه")]
    public void Ta_Marbuta_Becomes_Ha(string input, string expected) =>
        Assert.Equal(expected, ArabicTextNormalizer.Normalize(input));

    [Fact]
    public void Alef_Maksura_Becomes_Ya()
    {
        Assert.Equal("مستشفي", ArabicTextNormalizer.Normalize("مستشفى"));
    }

    [Fact]
    public void Tatweel_Is_Removed()
    {
        Assert.Equal("جميل", ArabicTextNormalizer.Normalize("جـــميل"));
    }

    [Fact]
    public void Two_Differently_Written_Spellings_Of_The_Same_Word_Normalize_Identically()
    {
        // Exactly the scenario docs/31 §4.2 and uq_items_company_name exist to catch: two
        // spellings a human reads as "the same product" must collapse to one search key.
        Assert.Equal(ArabicTextNormalizer.Normalize("جبنة"), ArabicTextNormalizer.Normalize("جبنه"));
    }

    [Fact]
    public void Latin_Text_And_Digits_Pass_Through_Unchanged()
    {
        Assert.Equal("ITM-2024", ArabicTextNormalizer.Normalize("ITM-2024"));
    }

    [Fact]
    public void Empty_String_Normalizes_To_Empty_String()
    {
        Assert.Equal(string.Empty, ArabicTextNormalizer.Normalize(string.Empty));
    }

    [Fact]
    public void Null_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ArabicTextNormalizer.Normalize(null!));
    }
}
