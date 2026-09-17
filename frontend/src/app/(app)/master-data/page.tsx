'use client';

import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { SimpleMasterDataScreen } from '@/components/features/simple-master-data-screen';

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
  return (
    <Tabs defaultValue="categories">
      <TabsList>
        <TabsTrigger value="categories">الأقسام</TabsTrigger>
        <TabsTrigger value="units">الوحدات</TabsTrigger>
      </TabsList>
      <TabsContent value="categories" className="mt-4">
        <SimpleMasterDataScreen<CategorySummary>
          title="الأقسام"
          basePath="/api/v1/categories"
          permissionCode="categories:manage"
          emptyMessage="لا توجد أقسام مطابقة"
          fields={[
            { key: 'nameArabic', label: 'الاسم', required: true },
            { key: 'description', label: 'الوصف' },
          ]}
        />
      </TabsContent>
      <TabsContent value="units" className="mt-4">
        <SimpleMasterDataScreen<UnitSummary>
          title="الوحدات"
          basePath="/api/v1/units"
          permissionCode="units:manage"
          emptyMessage="لا توجد وحدات مطابقة"
          fields={[
            { key: 'nameArabic', label: 'الاسم', required: true },
            { key: 'abbreviation', label: 'الاختصار' },
          ]}
        />
      </TabsContent>
    </Tabs>
  );
}
