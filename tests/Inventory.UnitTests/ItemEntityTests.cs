using Inventory.Domain.Entities;
using Xunit;

namespace Inventory.UnitTests;

/// <summary>Task 5.15 - the item entity's own normalization behaviour at construction and
/// update, without a database: the raw display value is never touched, only the derived search
/// key (docs/31 §4.2 point 2).</summary>
public sealed class ItemEntityTests
{
    [Fact]
    public void Constructing_An_Item_Preserves_The_Raw_Display_Name_And_Derives_The_Normalized_Key()
    {
        var item = new Item(Guid.NewGuid(), "ITM-000001", "جُبْنَة رومي", Guid.NewGuid(), Guid.NewGuid(), null, null, null);

        Assert.Equal("جُبْنَة رومي", item.NameArabic);
        Assert.Equal("جبنه رومي", item.NameNormalized);
    }

    [Fact]
    public void Updating_An_Item_Re_Derives_The_Normalized_Key_From_The_New_Name()
    {
        var item = new Item(Guid.NewGuid(), "ITM-000001", "جبنة", Guid.NewGuid(), Guid.NewGuid(), null, null, null);

        item.Update("جبنة رومي مستوردة", item.CategoryId, null, null, null);

        Assert.Equal("جبنة رومي مستوردة", item.NameArabic);
        Assert.Equal("جبنه رومي مستورده", item.NameNormalized);
    }

    [Fact]
    public void GeneratedCode_Is_Immutable_After_Construction()
    {
        var item = new Item(Guid.NewGuid(), "ITM-000042", "صنف", Guid.NewGuid(), Guid.NewGuid(), null, null, null);

        item.Update("اسم آخر", item.CategoryId, null, null, null);
        item.Deactivate();
        item.Reactivate();

        Assert.Equal("ITM-000042", item.GeneratedCode);
    }
}
