'use client';

import { SimpleMasterDataScreen } from '@/components/features/simple-master-data-screen';

interface SupplierSummary {
  id: string;
  nameArabic: string;
  phone: string | null;
  contactPerson: string | null;
  address: string | null;
  notes: string | null;
  isActive: boolean;
}

/** Task F3 (guide §8.2 "`/suppliers`"). */
export default function SuppliersPage() {
  return (
    <SimpleMasterDataScreen<SupplierSummary>
      title="الموردون"
      basePath="/api/v1/suppliers"
      permissionCode="suppliers:manage"
      emptyMessage="لا يوجد موردون مطابقون"
      fields={[
        { key: 'nameArabic', label: 'الاسم', required: true },
        { key: 'phone', label: 'الهاتف' },
        { key: 'contactPerson', label: 'مسؤول التواصل' },
        { key: 'address', label: 'العنوان' },
        { key: 'notes', label: 'ملاحظات' },
      ]}
    />
  );
}
