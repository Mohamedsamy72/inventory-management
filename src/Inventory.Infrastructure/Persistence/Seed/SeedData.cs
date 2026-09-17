using Inventory.Domain.Enums;

namespace Inventory.Infrastructure.Persistence.Seed;

/// <summary>
/// The five roles and the permission catalogue (docs/03 section 3), seeded via EF Core
/// <c>HasData</c> so they exist the moment the initial migration is applied (task 2.27). Ids are
/// fixed GUIDs, not <c>Guid.NewGuid()</c>, because <c>HasData</c> snapshots must be deterministic
/// across every migration regeneration - this is reference data the product ships with, not a
/// business record, so a fixed id is the correct choice here (unlike the "no hardcoded business
/// identifiers" rule, which is about tenant data).
/// <para>
/// <c>Accountant</c> is permanently retired (ADR-005) and deliberately absent. The four
/// non-grantable codes (<c>costs:view</c>, <c>valuation:view</c>, <c>audit:view</c>,
/// <c>audit:export</c>) are seeded only against <see cref="RoleName.Owner"/> - ADR-012 forbids
/// granting them to any other role or to an individual user, and <see cref="Domain.Entities.Permission.IsGrantable"/>
/// derives from the code itself, not from this seed.
/// </para>
/// </summary>
internal static class SeedData
{
    /// <summary>Fixed so `HasData` produces an identical snapshot on every migration regeneration.</summary>
    public static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly Guid OwnerRoleId = new("00000000-0000-0000-0a01-000000000001");
    public static readonly Guid AdminRoleId = new("00000000-0000-0000-0a01-000000000002");
    public static readonly Guid WarehouseStaffRoleId = new("00000000-0000-0000-0a01-000000000003");
    public static readonly Guid RestaurantSupervisorRoleId = new("00000000-0000-0000-0a01-000000000004");
    public static readonly Guid UserRoleId = new("00000000-0000-0000-0a01-000000000005");

    public sealed record RoleSeed(Guid Id, RoleName Name);

    public static readonly RoleSeed[] Roles =
    [
        new(OwnerRoleId, RoleName.Owner),
        new(AdminRoleId, RoleName.Admin),
        new(WarehouseStaffRoleId, RoleName.WarehouseStaff),
        new(RestaurantSupervisorRoleId, RoleName.RestaurantSupervisor),
        new(UserRoleId, RoleName.User),
    ];

    public sealed record PermissionSeed(Guid Id, string Code, string Description, string Module);

    private static Guid PermissionId(int sequence) => new($"00000000-0000-0000-0a02-{sequence:D12}");

    public static readonly PermissionSeed[] Permissions =
    [
        new(PermissionId(1), "items:view", "عرض الأصناف", "items"),
        new(PermissionId(2), "items:create", "إضافة صنف", "items"),
        new(PermissionId(3), "items:update", "تعديل صنف", "items"),
        new(PermissionId(4), "items:delete", "تعطيل صنف", "items"),
        new(PermissionId(5), "categories:manage", "إدارة الأقسام", "master_data"),
        new(PermissionId(6), "units:manage", "إدارة الوحدات", "master_data"),
        new(PermissionId(7), "suppliers:manage", "إدارة الموردين", "master_data"),
        new(PermissionId(8), "conversions:manage", "إدارة التحويلات", "master_data"),
        new(PermissionId(9), "warehouses:manage", "إدارة المستودعات", "warehouses_branches"),
        new(PermissionId(10), "restaurants:manage", "إدارة الفروع", "warehouses_branches"),
        new(PermissionId(11), "supply_requests:create", "إنشاء طلب توريد", "supply_requests"),
        new(PermissionId(12), "supply_requests:view", "عرض طلبات التوريد", "supply_requests"),
        new(PermissionId(13), "supply_requests:fulfill", "تلبية وشحن الطلب", "supply_requests"),
        new(PermissionId(14), "supplies:view", "عرض التوريدات", "supplies"),
        new(PermissionId(15), "supplies:dispatch", "إرسال الشحنة", "supplies"),
        new(PermissionId(16), "supplies:confirm", "تأكيد استلام الفرع", "supplies"),
        new(PermissionId(17), "receiving:view", "عرض الاستلام الوارد", "receiving"),
        new(PermissionId(18), "receiving:create", "إنشاء استلام", "receiving"),
        new(PermissionId(19), "receiving:submit", "ترحيل استلام", "receiving"),
        new(PermissionId(20), "receiving:verify", "مطابقة استلام", "receiving"),
        new(PermissionId(21), "receiving:reverse", "عكس استلام", "receiving"),
        new(PermissionId(22), "stock_counts:view", "عرض الجرد", "stock_counts"),
        new(PermissionId(23), "stock_counts:create", "بدء جرد جديد", "stock_counts"),
        new(PermissionId(24), "stock_counts:count", "تسجيل الكميات", "stock_counts"),
        new(PermissionId(25), "stock_counts:approve", "اعتماد الجرد والتسوية", "stock_counts"),
        new(PermissionId(26), "costs:view", "عرض التكاليف والأسعار", "financials"),
        new(PermissionId(27), "valuation:view", "عرض تقييم المخزون", "financials"),
        new(PermissionId(28), "audit:view", "عرض سجل التدقيق", "audit"),
        new(PermissionId(29), "audit:export", "تصدير سجل التدقيق", "audit"),
        new(PermissionId(30), "users:view", "عرض المستخدمين", "users"),
        new(PermissionId(31), "users:manage", "إدارة المستخدمين", "users"),
        new(PermissionId(32), "users:scope", "تعيين النطاقات", "users"),

        // Phase 12 (docs/09 §12.3-12.4) - not in docs/03 §3's original catalogue table, which has
        // no dedicated "Discrepancies" module row; added here following the same practice as
        // Phase 4's PrivilegeEscalationDenied/InvalidPassword (docs/13 §4: extend the catalogue
        // as a real requirement needs a code), and recorded in docs/27 §20 as a deliberate
        // addition.
        new(PermissionId(33), "discrepancies:view", "عرض الفروقات", "discrepancies"),
        new(PermissionId(34), "discrepancies:resolve", "حل الفروقات", "discrepancies"),
    ];

    private static readonly string[] OwnerCodes =
    [
        "items:view", "items:create", "items:update", "items:delete",
        "categories:manage", "units:manage", "suppliers:manage", "conversions:manage",
        "warehouses:manage", "restaurants:manage",
        "supply_requests:create", "supply_requests:view", "supply_requests:fulfill",
        "supplies:view", "supplies:dispatch", "supplies:confirm",
        "receiving:view", "receiving:create", "receiving:submit", "receiving:verify", "receiving:reverse",
        "stock_counts:view", "stock_counts:create", "stock_counts:count", "stock_counts:approve",
        "costs:view", "valuation:view",
        "audit:view", "audit:export",
        "users:view", "users:manage", "users:scope",
        "discrepancies:view", "discrepancies:resolve",
    ];

    private static readonly string[] AdminCodes =
    [
        "items:view", "items:create", "items:update", "items:delete",
        "categories:manage", "units:manage", "suppliers:manage", "conversions:manage",
        "warehouses:manage", "restaurants:manage",
        "supply_requests:create", "supply_requests:view", "supply_requests:fulfill",
        "supplies:view", "supplies:dispatch", "supplies:confirm",
        "receiving:view", "receiving:create", "receiving:submit", "receiving:verify", "receiving:reverse",
        "stock_counts:view", "stock_counts:create", "stock_counts:count", "stock_counts:approve",
        // costs/valuation/audit deliberately absent - Role Denial overrides even a stray grant
        // (ADR-012/ADR-006), so Admin never holds these regardless of what this seed does; they
        // are omitted here for clarity, not as the enforcement mechanism.
        "users:view", "users:manage", "users:scope",
        "discrepancies:view", "discrepancies:resolve",
    ];

    private static readonly string[] WarehouseStaffCodes =
    [
        "items:view",
        "supply_requests:view", "supply_requests:fulfill",
        "supplies:view", "supplies:dispatch",
        "receiving:view", "receiving:create", "receiving:submit", "receiving:verify",
        "stock_counts:view", "stock_counts:count",
        "discrepancies:view",
    ];

    private static readonly string[] RestaurantSupervisorCodes =
    [
        "items:view",
        "supply_requests:create", "supply_requests:view",
        "supplies:view", "supplies:confirm",
        "discrepancies:view",
    ];

    public sealed record RolePermissionSeed(Guid RoleId, Guid PermissionId);

    public static readonly RolePermissionSeed[] RolePermissions = BuildRolePermissions();

    private static RolePermissionSeed[] BuildRolePermissions()
    {
        Dictionary<string, Guid> permissionIdsByCode = Permissions.ToDictionary(p => p.Code, p => p.Id);

        IEnumerable<RolePermissionSeed> ForRole(Guid roleId, string[] codes) =>
            codes.Select(code => new RolePermissionSeed(roleId, permissionIdsByCode[code]));

        return
        [
            .. ForRole(OwnerRoleId, OwnerCodes),
            .. ForRole(AdminRoleId, AdminCodes),
            .. ForRole(WarehouseStaffRoleId, WarehouseStaffCodes),
            .. ForRole(RestaurantSupervisorRoleId, RestaurantSupervisorCodes),
        ];
    }
}
