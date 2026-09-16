/**
 * Phase 1 root route.
 *
 * The application shell, navigation and dashboards are Phase F1 and F2 work. This route
 * exists so the Next.js build has an entry point to compile and the E2E harness has a
 * page to load. It renders a static statement of fact and no data — there is no API
 * call here, and therefore no figure that could be mistaken for real (docs/12 section 2.3).
 */
export default function HomePage() {
  return (
    <main className="flex min-h-screen items-center justify-center p-8">
      <div className="max-w-lg rounded-lg border border-slate-200 bg-white p-8 text-center shadow-sm">
        <h1 className="text-2xl font-bold text-slate-900">نظام إدارة المخازن</h1>
        <p className="mt-3 text-sm text-slate-600">
          تم تجهيز البنية الأساسية للتطبيق. لم يتم بعد تنفيذ شاشات العمل.
        </p>
        <p className="mt-1 text-xs text-slate-500">
          المرحلة الأولى — الهيكل والبنية التحتية
        </p>
      </div>
    </main>
  );
}
