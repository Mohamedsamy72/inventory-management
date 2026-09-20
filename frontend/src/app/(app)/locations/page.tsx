'use client';

import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { SimpleMasterDataScreen } from '@/components/features/simple-master-data-screen';
import { RestaurantsScreen } from '@/components/features/restaurants-screen';

interface WarehouseSummary {
  id: string;
  nameArabic: string;
  code: string;
  address: string | null;
  description: string | null;
  isActive: boolean;
}

/** Task F3 (guide §8.2 "`/locations`"). Warehouses and restaurants share one screen
 * behind tabs - both are location master data. Neither carries a warehouse-selection
 * field on the restaurant side (Change 1 reversed ADR-028's single default serving
 * warehouse); the two tabs' forms differ only in their plain fields (code, address). */
export default function LocationsPage() {
  return (
    <Tabs defaultValue="warehouses">
      <TabsList>
        <TabsTrigger value="warehouses">المخازن</TabsTrigger>
        <TabsTrigger value="restaurants">الفروع</TabsTrigger>
      </TabsList>
      <TabsContent value="warehouses" className="mt-4">
        <SimpleMasterDataScreen<WarehouseSummary>
          title="المخازن"
          basePath="/api/v1/warehouses"
          permissionCode="warehouses:manage"
          emptyMessage="لا توجد مخازن مطابقة"
          fields={[
            { key: 'nameArabic', label: 'الاسم', required: true },
            { key: 'code', label: 'الكود', required: true, createOnly: true },
            { key: 'address', label: 'العنوان' },
            { key: 'description', label: 'الوصف' },
          ]}
          // Stock-balance view (/locations/[id]/stock) temporarily hidden from the UI on request -
          // the route itself is untouched, only this entry point is removed.
        />
      </TabsContent>
      <TabsContent value="restaurants" className="mt-4">
        <RestaurantsScreen />
      </TabsContent>
    </Tabs>
  );
}
