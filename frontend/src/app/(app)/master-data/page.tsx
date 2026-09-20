"use client";

import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { useSession } from "@/lib/auth/session-context";
import { ForbiddenState } from "@/components/feedback/forbidden-state";
import { SimpleMasterDataScreen } from "@/components/features/simple-master-data-screen";

interface CategorySummary {
  id: string;
  nameArabic: string;
  description: string | null;
  isActive: boolean;
}

interface UnitSummary {
  id: string;
  nameArabic: string;
  abbreviation: string | null;
  isActive: boolean;
}

/** Task F3 (docs/09 §Phase F3, guide §8.2 "`/master-data`"). Categories and units share
 * one screen behind tabs - both are simple named reference lists with no meaningful
 * cross-navigation of their own. */
export default function MasterDataPage() {
  const { profile } = useSession();
  // Only the tabs this account may use are offered; the first permitted one is the default.
  const canCategories =
    profile?.permissionCodes.includes("categories:manage") ?? true;
  const canUnits = profile?.permissionCodes.includes("units:manage") ?? true;
  if (!canCategories && !canUnits) {
    return <ForbiddenState reason="forbidden" />;
  }

  return (
    <Tabs defaultValue={canCategories ? "categories" : "units"}>
      <TabsList>
        {canCategories ? (
          <TabsTrigger value="categories">الأقسام</TabsTrigger>
        ) : null}
        {canUnits ? <TabsTrigger value="units">الوحدات</TabsTrigger> : null}
      </TabsList>
      {canCategories ? (
        <TabsContent value="categories" className="mt-4">
          <SimpleMasterDataScreen<CategorySummary>
            title="الأقسام"
            basePath="/api/v1/categories"
            permissionCode="categories:manage"
            emptyMessage="لا توجد أقسام مطابقة"
            fields={[
              { key: "nameArabic", label: "الاسم", required: true },
              { key: "description", label: "الوصف" },
            ]}
          />
        </TabsContent>
      ) : null}
      {canUnits ? (
        <TabsContent value="units" className="mt-4">
          <SimpleMasterDataScreen<UnitSummary>
            title="الوحدات"
            basePath="/api/v1/units"
            permissionCode="units:manage"
            emptyMessage="لا توجد وحدات مطابقة"
            fields={[
              { key: "nameArabic", label: "الاسم", required: true },
              { key: "abbreviation", label: "الاختصار" },
            ]}
          />
        </TabsContent>
      ) : null}
    </Tabs>
  );
}
