using System.Text;

namespace Inventory.Domain.Common;

/// <summary>
/// Produces the search/uniqueness key for Arabic text (docs/31 section 4.2): strips diacritics
/// and tatweel, unifies alef forms and the ta-marbuta/alef-maksura variants. The display value
/// is never touched - only this derived key is - so what the user typed is always what they see
/// back (docs/31 section 4.2 point 2: "for the search key only, never for the stored display value").
/// </summary>
public static class ArabicTextNormalizer
{
    private const char Tatweel = 'ـ';

    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            // Arabic diacritics (harakat, sukun, shadda, tanween): U+064B-U+0652, and the
            // superscript alef U+0670. Stripped entirely - they carry no distinguishing weight
            // for search/uniqueness.
            if (c is >= 'ً' and <= 'ْ' or 'ٰ')
            {
                continue;
            }

            if (c == Tatweel)
            {
                continue;
            }

            char normalized = c switch
            {
                'آ' or 'أ' or 'إ' => 'ا', // آ, أ, إ -> ا
                'ة' => 'ه',                          // ة -> ه
                'ى' => 'ي',                          // ى -> ي
                _ => c,
            };

            builder.Append(normalized);
        }

        return builder.ToString();
    }
}
