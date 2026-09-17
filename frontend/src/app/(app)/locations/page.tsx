'use client';

import { useRouter } from 'next/navigation';
import { Warehouse } from 'lucide-react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
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
 * behind tabs - both are location master data, but their forms genuinely differ
 * (a restaurant requires a serving-warehouse selection; a warehouse does not). */
export default function LocationsPage() {
  const router = useRouter();

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
          rowActions={(row) => (
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={() => router.push(`/locations/${row.id}/stock`)}
              aria-label="عرض رصيد المخزون"
            >
              <Warehouse />
            </Button>
          )}
        />
      </TabsContent>
      <TabsContent value="restaurants" className="mt-4">
        <RestaurantsScreen />
      </TabsContent>
    </Tabs>
  );
}
