'use client';

import Link from 'next/link';
import { Plus } from 'lucide-react';
import { useSession } from '@/lib/auth/session-context';
import { Button } from '@/components/ui/button';
import { LoadingSkeleton } from '@/components/feedback/loading-skeleton';
import { useState } from 'react';
import { CreateSupplyRequestDialog } from '@/components/features/create-supply-request-dialog';
import { DashboardWidget } from '@/components/features/dashboard-widget';
import { WarehouseInventoryCards } from '@/components/features/warehouse-inventory-cards';

interface ReceivingOrderSummary {
  id: string;
  documentNumber: string;
  status: 'Draft' | 'Submitted' | 'Verified' | 'Reversed';
}

interface SupplyRequestSummary {
  id: string;
  documentNumber: string;
  status: 'Draft' | 'Submitted' | 'PartiallyFulfilled' | 'Fulfilled' | 'Cancelled';
}

interface SupplySummary {
  id: string;
  documentNumber: string;
  status: 'Prepared' | 'Dispatched' | 'Confirmed' | 'ConfirmedWithDiscrepancy' | 'RejectedAtDelivery' | 'Cancelled';
}

interface DiscrepancySummary {
  id: string;
  documentNumber: string;
  status: 'Open' | 'Investigating' | 'Resolved';
}

const isOpenDiscrepancy = (d: DiscrepancySummary) => d.status !== 'Resolved';
const renderDoc = (item: { documentNumber: string }) => item.documentNumber;

/**
 * Task F6 (docs/09 §Phase F6, docs/12 §2). Every widget is a real, live API count - no
 * hardcoded zero, no mock array, no placeholder chart (docs/12 §2.3, ADR-029). Owner/Admin
 * see the same three widget families as Warehouse Staff/Restaurant Supervisor, just
 * tenant-wide rather than scoped - docs/09/docs/12 specify no separate Owner/Admin widget
 * set beyond "an overview", and reusing the same real, already-tested queries is more
 * honest than inventing bespoke Owner-only figures with no specification behind them.
 */
export default function DashboardPage() {
  const { profile } = useSession();
  const [createOpen, setCreateOpen] = useState(false);

  if (!profile) {
    return <LoadingSkeleton className="h-40 w-full" />;
  }

  if (profile.role === 'WarehouseStaff') {
    return (
      <div className="flex flex-col gap-4">
        <div>
          <h1 className="font-heading text-xl font-medium text-foreground">مهام المخزن</h1>
          <p className="text-sm text-muted-foreground">ما الذي يحتاج إلى تلبية أو استلام الآن في المخزن؟</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild>
            <Link href="/receiving">
              <Plus />
              استلام وارد جديد
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/supply-requests">معالجة طلبيات المطعم</Link>
          </Button>
        </div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <DashboardWidget<SupplyRequestSummary>
            title="الطلبيات الواردة من المطعم بانتظار التجهيز"
            fetchPath="/api/v1/supply-requests"
            filter={(r) => r.status === 'Submitted' || r.status === 'PartiallyFulfilled'}
            renderItem={renderDoc}
            emptyMessage="لا توجد طلبيات معلقة حالياً"
            viewAllHref="/supply-requests"
          />
          <DashboardWidget<ReceivingOrderSummary>
            title="شحنات الموردين الداخلة الى المخزن"
            fetchPath="/api/v1/receiving-orders"
            filter={(r) => r.status === 'Draft' || r.status === 'Submitted'}
            renderItem={renderDoc}
            emptyMessage="لا توجد شحنات قيد المعالجة"
            viewAllHref="/receiving"
          />
          <DashboardWidget<DiscrepancySummary>
            title="فروقات الاستلام المفتوحة"
            fetchPath="/api/v1/discrepancies"
            filter={isOpenDiscrepancy}
            renderItem={renderDoc}
            emptyMessage="لا توجد فروقات مفتوحة"
            viewAllHref="/discrepancies"
          />
        </div>
      </div>
    );
  }

  if (profile.role === 'RestaurantSupervisor') {
    return (
      <div className="flex flex-col gap-4">
        <div>
          <h1 className="font-heading text-xl font-medium text-foreground">مهام الفرع</h1>
          <p className="text-sm text-muted-foreground">ما الذي يجب أن أطلبه أو أؤكد استلامه اليوم للفرع؟</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => setCreateOpen(true)}>
            <Plus />
            طلب بضاعة جديد
          </Button>
          <Button asChild variant="outline">
            <Link href="/supplies">تأكيد استلام البضاعة</Link>
          </Button>
        </div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <DashboardWidget<SupplySummary>
            title="الصادر من المخزن بانتظار تأكيد الاستلام"
            fetchPath="/api/v1/supplies"
            filter={(s) => s.status === 'Dispatched'}
            renderItem={renderDoc}
            emptyMessage="لا توجد شحنات بانتظار التأكيد"
            viewAllHref="/supplies"
          />
          <DashboardWidget<SupplyRequestSummary>
            title="طلبات البضاعة المقدمة للمخزن"
            fetchPath="/api/v1/supply-requests"
            filter={(r) => r.status === 'Submitted' || r.status === 'PartiallyFulfilled' || r.status === 'Fulfilled'}
            renderItem={renderDoc}
            emptyMessage="لا توجد طلبات نشطة حالياً"
            viewAllHref="/supply-requests"
          />
          <DashboardWidget<DiscrepancySummary>
            title="فروقات استلام معلقة"
            fetchPath="/api/v1/discrepancies"
            filter={isOpenDiscrepancy}
            renderItem={renderDoc}
            emptyMessage="لا توجد فروقات معلقة"
            viewAllHref="/discrepancies"
          />
        </div>
        <CreateSupplyRequestDialog open={createOpen} onOpenChange={setCreateOpen} />
      </div>
    );
  }

  if (profile.role === 'User' || profile.role === null) {
    // docs/03 §1 point 5: "User" is a base authenticated account with ZERO default
    // capabilities - access exists only through explicit UserPermission grants, which
    // vary per account and have no fixed widget set docs/12 could specify. Showing the
    // Owner/Admin overview's title and widgets here would be actively misleading (every
    // widget would 403 for an ungranted account) - and inventing a bespoke "User
    // dashboard" figure with no specification behind it is exactly what docs/12 §2.3
    // forbids. The permitted screens, if any, are reachable via the sidebar
    // (`getNavItemsForRole` already renders nothing extra for this role beyond what its
    // real permissions unlock elsewhere in the app).
    return (
      <div className="flex flex-col gap-4">
        <h1 className="font-heading text-xl font-medium text-foreground">مرحباً بك</h1>
        <p className="text-sm text-muted-foreground">
          لا توجد صلاحيات افتراضية لهذا الحساب. استخدم القائمة الجانبية للوصول إلى الشاشات المصرح بها لك، إن وجدت.
        </p>
      </div>
    );
  }

  // Owner / Admin: the same three real, tenant-wide widget families.
  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="font-heading text-xl font-medium text-foreground">
          {profile.role === 'Owner' ? 'لوحة التحكم الرئيسية' : 'لوحة التحكم التشغيلية'}
        </h1>
        <p className="text-sm text-muted-foreground">نظرة عامة على العمليات الجارية في كل المخازن والفروع.</p>
      </div>
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <DashboardWidget<SupplyRequestSummary>
          title="طلبات البضاعة قيد التنفيذ"
          fetchPath="/api/v1/supply-requests"
          filter={(r) => r.status === 'Submitted' || r.status === 'PartiallyFulfilled'}
          renderItem={renderDoc}
          emptyMessage="لا توجد طلبات قيد التنفيذ"
          viewAllHref="/supply-requests"
        />
        <DashboardWidget<ReceivingOrderSummary>
          title="أوامر الاستلام قيد المعالجة"
          fetchPath="/api/v1/receiving-orders"
          filter={(r) => r.status === 'Draft' || r.status === 'Submitted'}
          renderItem={renderDoc}
          emptyMessage="لا توجد أوامر استلام قيد المعالجة"
          viewAllHref="/receiving"
        />
        <DashboardWidget<DiscrepancySummary>
          title="الفروقات المفتوحة"
          fetchPath="/api/v1/discrepancies"
          filter={isOpenDiscrepancy}
          renderItem={renderDoc}
          emptyMessage="لا توجد فروقات مفتوحة"
          viewAllHref="/discrepancies"
        />
      </div>

      {profile.role === 'Owner' ? (
        <div className="flex flex-col gap-3">
          <div>
            <h2 className="font-heading text-lg font-medium text-foreground">رصيد المخازن</h2>
            <p className="text-sm text-muted-foreground">إجمالي الأصناف والقيمة لكل مخزن - اضغط على مخزن لعرض تفاصيل الأصناف والأسعار.</p>
          </div>
          <WarehouseInventoryCards />
        </div>
      ) : null}
    </div>
  );
}
