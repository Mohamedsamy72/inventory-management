/**
 * Task F2 - the universal post-login landing route. The real role-specific action
 * dashboards (docs/12 section 2) are Phase F6; this exists only so `/dashboard` is a
 * real page every role's nav can link to today, with zero fake data - a static
 * statement of fact and no invented figure (docs/12 section 2.3).
 */
export default function DashboardPage() {
  return (
    <div className="rounded-lg border border-dashed border-border p-8 text-center text-muted-foreground">
      لوحات المهام التشغيلية لكل دور قادمة في مرحلة لاحقة من المشروع.
    </div>
  );
}
